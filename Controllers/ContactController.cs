using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels;
using Stripe;
using Stripe.Checkout;

namespace Seonyx.Web.Controllers
{
    public class ContactController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        // GET /contact
        public ActionResult Index()
        {
            ViewBag.Title = "Contact Us";
            return View(new PaidContactViewModel());
        }

        // POST /contact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(PaidContactViewModel model)
        {
            // Honeypot: bots fill in the hidden Website field
            if (!string.IsNullOrEmpty(model.Website))
            {
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Contact Us";
                return View(model);
            }

            // Persist submission in Pending state before redirecting to Stripe
            var submission = new PaidContactSubmission
            {
                ReferenceId   = Guid.NewGuid().ToString(),
                Name          = model.Name.Trim(),
                Email         = model.Email.Trim(),
                Company       = string.IsNullOrWhiteSpace(model.Company) ? null : model.Company.Trim(),
                Subject       = model.Subject.Trim(),
                Message       = model.Message.Trim(),
                IpAddress     = Request.UserHostAddress,
                UserAgent     = Request.UserAgent != null && Request.UserAgent.Length > 500
                                    ? Request.UserAgent.Substring(0, 500)
                                    : Request.UserAgent,
                Status        = "Pending",
                SubmittedDate = DateTime.UtcNow
            };

            db.PaidContactSubmissions.Add(submission);
            db.SaveChanges();

            // Create Stripe Checkout Session
            try
            {
                StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];

                var baseUrl = string.Format("{0}://{1}", Request.Url.Scheme, Request.Url.Authority);
                var feeAmount = long.Parse(ConfigurationManager.AppSettings["ContactFeeAmountCents"] ?? "200");

                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency   = "eur",
                                UnitAmount = feeAmount,
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name        = "Contact Fee",
                                    Description = "One-time fee to submit your enquiry to seonyx.com"
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode          = "payment",
                    SuccessUrl    = baseUrl + "/contact/success?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl     = baseUrl + "/contact/cancel",
                    CustomerEmail = submission.Email,
                    Metadata = new Dictionary<string, string>
                    {
                        { "reference_id", submission.ReferenceId }
                    },
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            { "reference_id", submission.ReferenceId }
                        }
                    }
                };

                var service = new SessionService();
                var session = service.Create(options);

                submission.StripeCheckoutSessionId = session.Id;
                db.SaveChanges();

                return Redirect(session.Url);
            }
            catch (StripeException ex)
            {
                ModelState.AddModelError("", "Payment could not be initiated: " + ex.StripeError.Message);
                ViewBag.Title = "Contact Us";
                return View(model);
            }
        }

        // GET /contact/success?session_id=
        public ActionResult Success(string session_id)
        {
            var vm = new ContactSuccessViewModel
            {
                CheckoutSessionId = session_id
            };

            if (!string.IsNullOrEmpty(session_id))
            {
                var sub = db.PaidContactSubmissions
                    .FirstOrDefault(s => s.StripeCheckoutSessionId == session_id);
                if (sub != null)
                {
                    vm.Email           = sub.Email;
                    vm.PaymentIntentId = sub.StripePaymentIntentId;
                }
            }

            ViewBag.Title = "Payment Confirmed";
            return View(vm);
        }

        // GET /contact/cancel
        public ActionResult Cancel()
        {
            ViewBag.Title = "Payment Cancelled";
            return View();
        }

        // Called by the Stripe webhook controller after verifying the event
        internal void SendPaidContactEmail(PaidContactSubmission submission)
        {
            try
            {
                var contactEmail = ConfigurationManager.AppSettings["ContactEmail"];
                var smtpHost     = ConfigurationManager.AppSettings["SmtpHost"];
                var smtpPort     = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                var smtpUser     = ConfigurationManager.AppSettings["SmtpUsername"];
                var smtpPass     = ConfigurationManager.AppSettings["SmtpPassword"];
                var enableSsl    = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");

                if (string.IsNullOrEmpty(smtpHost) || smtpHost == "smtp.example.com")
                    return;

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl    = enableSsl;
                    client.Credentials  = new NetworkCredential(smtpUser, smtpPass);

                    var subject = string.Format("[Seonyx Paid Enquiry] {0} -- Ref: {1}",
                        submission.Subject,
                        submission.StripePaymentIntentId ?? submission.ReferenceId);

                    var body = string.Format(
                        "PAID CONTACT SUBMISSION\r\n" +
                        "=======================\r\n\r\n" +
                        "Name:    {0}\r\n" +
                        "Email:   {1}\r\n" +
                        "Company: {2}\r\n" +
                        "Subject: {3}\r\n\r\n" +
                        "Message:\r\n{4}\r\n\r\n" +
                        "---\r\n" +
                        "Payment:    EUR {5:0.00}\r\n" +
                        "Payment ID: {6}\r\n" +
                        "Reference:  {7}\r\n" +
                        "Submitted:  {8} UTC\r\n" +
                        "Processed:  {9} UTC\r\n" +
                        "IP Address: {10}",
                        submission.Name,
                        submission.Email,
                        submission.Company ?? "(not provided)",
                        submission.Subject,
                        submission.Message,
                        (submission.AmountPaid ?? 0) / 100.0,
                        submission.StripePaymentIntentId ?? "(pending)",
                        submission.ReferenceId,
                        submission.SubmittedDate,
                        submission.ProcessedDate,
                        submission.IpAddress ?? "(unknown)");

                    var message = new MailMessage(smtpUser, contactEmail, subject, body);
                    message.ReplyToList.Add(new MailAddress(submission.Email, submission.Name));

                    client.Send(message);
                }
            }
            catch (Exception)
            {
                // Do not propagate -- webhook must return 200 even if email fails
            }
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
