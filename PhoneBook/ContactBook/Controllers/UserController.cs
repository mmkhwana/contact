using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using ContactBook.Models;

namespace ContactBook.Controllers
{
    public class UserController : Controller
    {
       
        //Registration Action 
        [HttpGet]
        public ActionResult Registration()
        {
            return View();
        }

        #region Registration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registration([Bind(Exclude = "IsEmailVerified, ActivationCode")] User user)
        {
            bool Status = false;
            string message = "";
            //Model Validation
            if (ModelState.IsValid)
            {

                #region //Email Exists

                var isExist = IsEmailExist(user.Email);
                if (isExist)
                {
                    ModelState.AddModelError("EmailExist", "Email already exist");
                    return View(user);
                }
                #endregion


                #region Generate Activation Code
                user.ActivationCode = Guid.NewGuid();
                #endregion

                #region Password Hashing
                user.Password = Crypto.Hash(user.Password);
                user.ConfirmPassword = Crypto.Hash(user.ConfirmPassword);
                #endregion
                user.IsEmailVerified = false;

                #region Save to Database
                using (UserDBEntities connect = new UserDBEntities())
                {
                    connect.Users.Add(user);
                    connect.SaveChanges();

                    //Send Email to User
                    SendVerificationLinkEmail(user.Email, user.ActivationCode.ToString());
                    message = "Registration successfully done. Account activation link " +
                        " has been sent to your Email:" + user.Email;
                    Status = true;
                }
                #endregion
            }
            else
            {
                message = "Invalid Request";
            }

            ViewBag.Message = message;
            ViewBag.Status = Status;
            return View(user);
        }
        #endregion
        #region verify account
        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool Status = false;
            using (UserDBEntities connect = new UserDBEntities())
            {
                connect.Configuration.ValidateOnSaveEnabled = false;

                var account = connect.Users.Where(a => a.ActivationCode == new Guid(id)).FirstOrDefault();
                if (account != null)
                {
                    account.IsEmailVerified = true;
                    connect.SaveChanges();
                    Status = true;
                }
                else
                {
                    ViewBag.Message = "Invalid Request";
                }
            }
            ViewBag.Status = Status;
            return View();
        }
        #endregion

        #region isemail

        [NonAction]
        public bool IsEmailExist(string emailID)
        {
            using (UserDBEntities connect = new UserDBEntities())
            {
                var v = connect.Users.Where(a => a.Email == emailID).FirstOrDefault();
                return v != null;
            }
        }
        #endregion

        #region Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserLogin login, string ReturnUrl = "")
        {
            string message = "";
            using (UserDBEntities connect = new UserDBEntities())
            {
                var account = connect.Users.Where(attribute => attribute.Email == login.Email).FirstOrDefault();
                if (account != null)
                {
                    if (!account.IsEmailVerified)
                    {
                        ViewBag.Message = "Please verify your email first";
                        return View();
                    }
                    if (string.Compare(Crypto.Hash(login.Password), account.Password) == 0)
                    {
                        int timeout = login.RememberMe ? 525600 : 20; //1 year 
                        var ticket = new FormsAuthenticationTicket(login.Email, login.RememberMe, timeout);
                        string encrypted = FormsAuthentication.Encrypt(ticket);
                        var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                        cookie.Expires = DateTime.Now.AddMinutes(timeout);
                        cookie.HttpOnly = true;
                        Response.Cookies.Add(cookie);


                        if (Url.IsLocalUrl(ReturnUrl))
                        {
                            return Redirect(ReturnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Contacts");
                        }
                    }
                    else
                    {
                        message = "Invalid data provided";
                    }
                }
                else
                {
                    message = "Invalid data provided";
                }
            }
            ViewBag.Message = message;
            return View();
        }
        #endregion
        #region Lougout
        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }
        #endregion

        #region send verification email
        [NonAction]
        public void SendVerificationLinkEmail(string emailID, string activationCode, string emailfor = "VerifyAccount")
        {
            var verifyUrl = "/User/"+emailfor+"/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, verifyUrl);

            var fromEmail = new MailAddress("ml.modisadife@gmail.com", "Contact Book Account");
            var toEmail = new MailAddress(emailID);
            var fromEmailPassword = "1792fd2749bb31";

            string subject = "";
            string body = "";
            if (emailfor == "VerifyAccount")
            {
                subject = "Your account is successfully created! Please Confirm.";

                body = "<br/><br/>We are excited to tell you that you may now use your Contact Book. Please click on the below link to verify your email and start saving contacts" +
                   " <br/><br/><a href='" + link + "'>" + link + "</a> ";
            }
            else 
            {
                subject = "Reset Password";
                body = "Hi,<br/><br>We got a request for reset your account password.Please Click link below to reset"+
                    "<br/><br><a href="+link+">Reset Password Link</a>";
            }

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, fromEmailPassword)
            };

            using (var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
                smtp.Send(message);
        }
        #endregion

        #region Forget Password

        public ActionResult FogertPassword()
        {
            return View(); 
        }

        [HttpPost]
        public ActionResult FogertPassword(string EmailID)
        {
            //verify email
            Console.WriteLine(EmailID);
            string message = "";
            bool status = false;

            using (UserDBEntities dataconnect = new UserDBEntities())
            {
                var account = dataconnect.Users.Where(value => value.Email == EmailID).FirstOrDefault();
                if (account != null)
                {
                    //Send email for reset password
                    string resetCode = Guid.NewGuid().ToString();
                    SendVerificationLinkEmail(account.Email,resetCode, "ResetPassword");
                    account.ResetPassword = resetCode;
                    //
                    dataconnect.Configuration.ValidateOnSaveEnabled = status;
                    dataconnect.SaveChanges();
                    message = "Reset Password link has been sent to your email id.";

                }
                else
                {
                    message = "Account Not Found";
                }
            }
            ViewBag.Message = message;
            return View();
        }
        #endregion

        #region GeneratePassword

        public ActionResult ResetPassword(string id)
        {
            using (UserDBEntities data = new UserDBEntities())
            {
                var user = data.Users.Where(value => value.ResetPassword == id).FirstOrDefault();
                if (user != null) 
                {
                    ResetPassworsModel model = new ResetPassworsModel();
                    model.ResetCode = id;
                    return View(model);
                }
                else 
                {
                    return HttpNotFound();
                }
            }
        }

        #endregion

        #region
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPassworsModel model)
        {
            var message = "";

            if (ModelState.IsValid)
            {
                using (UserDBEntities data = new UserDBEntities())
                {
                    var user = data.Users.Where(value => value.ResetPassword == model.ResetCode).FirstOrDefault();
                    if (user != null)
                    {
                        user.Password = Crypto.Hash(model.NewPassword);
                        user.ResetPassword = "";
                        data.Configuration.ValidateOnSaveEnabled = false;
                        data.SaveChanges();
                        message = "New Password Updated successfully";
                    }
                }
            }
            else 
            {
                message = "Somthing Invalide";
            }
            ViewBag.Message = message;
            return View(model);
        }
        #endregion

    }
}
