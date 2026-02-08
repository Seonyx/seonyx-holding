using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels;

namespace Seonyx.Web.Controllers
{
    public class ContactController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index()
        {
            ViewBag.Title = "Contact Us";
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(ContactViewModel model)
        {
            // Honeypot check - if the hidden field is filled, it's a bot
            if (!string.IsNullOrEmpty(model.Website))
            {
                return RedirectToAction("Success");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Contact Us";
                return View(model);
            }

            var submission = new ContactSubmission
            {
                Name = model.Name,
                Email = model.Email,
                Subject = model.Subject,
                Message = model.Message,
                IpAddress = Request.UserHostAddress,
                UserAgent = Request.UserAgent,
                SubmittedDate = DateTime.Now
            };

            db.ContactSubmissions.Add(submission);
            db.SaveChanges();

            SendNotificationEmail(submission);

            return RedirectToAction("Success");
        }

        public ActionResult Success()
        {
            ViewBag.Title = "Message Sent";
            return View();
        }

        private void SendNotificationEmail(ContactSubmission submission)
        {
            try
            {
                var contactEmail = ConfigurationManager.AppSettings["ContactEmail"];
                var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
                var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                var smtpUser = ConfigurationManager.AppSettings["SmtpUsername"];
                var smtpPass = ConfigurationManager.AppSettings["SmtpPassword"];
                var enableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");

                if (string.IsNullOrEmpty(smtpHost) || smtpHost == "smtp.example.com")
                    return;

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var subject = string.IsNullOrEmpty(submission.Subject)
                        ? "New Contact Form Submission"
                        : "Contact: " + submission.Subject;

                    var body = string.Format(
                        "Name: {0}\nEmail: {1}\nSubject: {2}\n\nMessage:\n{3}\n\nSubmitted: {4}\nIP: {5}",
                        submission.Name,
                        submission.Email,
                        submission.Subject ?? "(none)",
                        submission.Message,
                        submission.SubmittedDate,
                        submission.IpAddress);

                    var message = new MailMessage(smtpUser, contactEmail, subject, body);
                    message.ReplyToList.Add(new MailAddress(submission.Email, submission.Name));

                    client.Send(message);
                }
            }
            catch (Exception)
            {
                // Log error but don't fail the form submission
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
