using System;
using System.IO;
using System.Web.Mvc;
using System.Configuration;
using Seonyx.Web.Models;
using Stripe;
using Stripe.Checkout;

namespace Seonyx.Web.Controllers
{
    // This controller has no authentication or antiforgery -- it is called by Stripe,
    // not by a browser. Signature verification via EventUtility.ConstructEvent() is
    // the only authentication mechanism; never remove it.
    public class StripeWebhookController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        [HttpPost, AllowAnonymous, ValidateInput(false)]
        public ActionResult Receive()
        {
            string json;
            Request.InputStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(Request.InputStream))
            {
                json = reader.ReadToEnd();
            }

            var stripeSignature = Request.Headers["Stripe-Signature"];
            var webhookSecret   = ConfigurationManager.AppSettings["StripeWebhookSecret"];

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret,
                    throwOnApiVersionMismatch: false);
            }
            catch (StripeException)
            {
                // Bad signature -- reject immediately
                return new HttpStatusCodeResult(400);
            }

            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session == null)
                    return new HttpStatusCodeResult(200);

                string referenceId;
                if (!session.Metadata.TryGetValue("reference_id", out referenceId) || string.IsNullOrEmpty(referenceId))
                    return new HttpStatusCodeResult(200);

                var submission = db.PaidContactSubmissions
                    .FirstOrDefault(s => s.ReferenceId == referenceId);

                if (submission != null && submission.Status == "Pending")
                {
                    submission.Status               = "Completed";
                    submission.StripePaymentIntentId = session.PaymentIntentId;
                    submission.AmountPaid           = (int)(session.AmountTotal ?? 0);
                    submission.ProcessedDate        = DateTime.UtcNow;
                    db.SaveChanges();

                    // Send email via ContactController's shared helper
                    var contactCtrl = new ContactController();
                    contactCtrl.ControllerContext = ControllerContext;
                    contactCtrl.SendPaidContactEmail(submission);
                }
            }

            return new HttpStatusCodeResult(200);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
