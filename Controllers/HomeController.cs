using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Net.Mail;
using System.Net;
using System.Reflection;
using XClone.Models;
using Microsoft.Extensions.Hosting;

namespace XClone.Controllers
{
    public class HomeController : Controller
    {
        string connectionString = "";
        public bool CheckLogin()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("username")))
            {
                return false;
            }
            return true;
        }
        public IActionResult Index(string? MessageCssClass, string? Message)
        {
            ViewData["Title"] = "Ana Sayfa";
            ViewData["username"] = HttpContext.Session.GetString("username");
            ViewData["Id"] = HttpContext.Session.GetInt32("Id");
            var index = new PostModel();
            using var connection = new SqlConnection(connectionString);
            var twt = connection.Query<Post>("SELECT haber.Id, Summary, kullanicilar.Username, haber.CreatedDate, kullanicilar.ImgPath as ImgPath, haber.ImgPath as TwtImg FROM haber LEFT JOIN kullanicilar on haber.UserId = kullanicilar.Id  ORDER BY haber.CreatedDate DESC");


            ViewBag.Message = Message;
            ViewBag.MessageCssClass = MessageCssClass;
            return View(twt);
        }
        

        [HttpPost]
        [Route("/haberekle")]
        public IActionResult HaberEkle(Post model)
        {
            int? userId = HttpContext.Session.GetInt32("Id");

            if (userId == null)
            {
                ViewBag.mesaj = "Kullanıcı kimliği bulunamadı. Lütfen tekrar giriş yapın.";
                return View("postadd", model);
            }

            ViewData["Id"] = userId;
            ViewData["username"] = HttpContext.Session.GetString("username");

            model.UserId = userId.Value;

            if (model.Img != null && model.Img.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Img.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);

                using var fileStream = new FileStream(filePath, FileMode.Create);
                model.Img.CopyTo(fileStream);
                model.ImgPath = fileName;
            }
            model.CreatedDate = DateTime.Now;
            model.Slug = CreateSlug(model.Summary);

            using var connection = new SqlConnection(connectionString);
            var sql = "INSERT INTO haber (Summary, ImgPath, Slug, CreatedDate, UserId) VALUES (@Summary, @ImgPath, @Slug, @CreatedDate, @UserId)";
            var data = new
            {
                model.Summary,
                model.ImgPath,
                model.Slug,
                model.CreatedDate,
                model.UserId
            };
            var rowsAffected = connection.Execute(sql, data);
            return RedirectToAction("Index");
        }

        [Route("/habersil/{id}")]
        public IActionResult HaberSil(int id)
        {
            using var connection = new SqlConnection(connectionString);
            var sql = "DELETE FROM haber WHERE Id = @Id";
            connection.Execute(sql, new { Id = id});
            ViewBag.Message = "Gönderi silindi!";
            ViewBag.MessageCssClass = "alert-success";
            return View("Message");
        }

        [Route("/detay/{id}")]
        public IActionResult Details(int? id)
        {
            ViewData["Title"] = "Profil";
            if (id == null)
            {
                ViewBag.Message = "Böyle bir gönderi yok!";
                ViewBag.MessageCssClass = "alert-danger";
                return View("Message");
            }
            ViewBag.Yorum = true;
            if (!CheckLogin())
            {
                ViewBag.Yorum = false;

            }
          
            using var connection = new SqlConnection(connectionString);
            var postModel = new PostModel();
            var sql = @"
                SELECT haber.Id ,haber.UserId ,summary, kullanicilar.Username, haber.CreatedDate, kullanicilar.ImgPath, haber.ImgPath as TwtImg  FROM haber LEFT JOIN kullanicilar on haber.UserId = kullanicilar.Id WHERE haber.Id = @id;

                SELECT kullanicilar.Username, kullanicilar.ImgPath, comments.Comment, comments.CreatedDate, comments.Id FROM comments LEFT JOIN kullanicilar on comments.UserId = kullanicilar.Id  WHERE PostId = @id ORDER BY comments.CreatedDate DESC";
            using var multi = connection.QueryMultiple(sql, new {id });
            var post = multi.ReadFirstOrDefault<Post>();
            var comment = multi.Read<PostCommet>().ToList();
            postModel.Post = post;
            postModel.Comments = comment;

            if (postModel.Post.UserId == HttpContext.Session.GetInt32("Id"))
            {
                ViewBag.yetki = "Full";
            }
            ViewBag.LogName = HttpContext.Session.GetString("username");
            return View(postModel);

        }
        [HttpPost]
        [Route("/YorumEkle")]
        public IActionResult YorumEkle(PostCommet model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Index");
            }
            model.UserId = (int)HttpContext.Session.GetInt32("Id");
            model.CreatedDate = DateTime.Now;

            using var connection = new SqlConnection(connectionString);
            model.CreatedDate = DateTime.Now;
            var sql = "INSERT INTO comments (Comment, CreatedDate, UserId, PostId) VALUES (@Comment, @CreatedDate, @UserId, @PostId)";
            var affectedRows = connection.Execute(sql, model);

            var postOwnerEmail = connection.QueryFirstOrDefault<string>("SELECT Email FROM kullanicilar WHERE Id = (SELECT UserId FROM haber WHERE Id = @PostId)", new { model.PostId });

            ViewBag.SuccessMessage = "Kayıt bilgileri mail adresinize gönderilmiştir";


            return RedirectToAction("Details", new {id = model.PostId});
        }

        [Route("/YorumSil/{id}")]
        public IActionResult CommentDel(int id)
        {
            using var connection = new SqlConnection(connectionString);


            var sql = "DELETE FROM comments WHERE Id = @Id";
            connection.Execute(sql, new { Id = id });
            ViewBag.Message = "Yorum silindi!";
            ViewBag.MessageCssClass = "alert-danger";
            return View("Message");
        }
        private string CreateSlug(string title)
        {
            return title.ToLower().Replace(" ", "-").Replace("ö", "o").Replace("ü", "u").Replace("ç", "c").Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g");
        }


        public IActionResult Register()
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
            ViewBag.AuthError = TempData["AuthError"] as string;
            return View(new User());
        }

        [HttpPost]
        [Route("/kayit")]
        public IActionResult Kayit(User model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Eksik alan bulunuyor!";
                return View("register", model);
            }
            if (!string.Equals(model.Password, model.PasswordRepeat))
            {
                ViewBag.ErrorMessage = "Şifreler uyuşmuyor!";
                return View("register", model);
            }
            
            using var connection = new SqlConnection(connectionString);
            var login = connection.QueryFirstOrDefault<User>("SELECT * FROM kullanicilar WHERE UserName = @UserName", new { model.UserName }); //kullanıcı adı kontrol
            if (login != null)
            {
                //Burada mevcut ise hata gösteriyorum
                ViewBag.ErrorMessage = "Bu kullanıcı adı zaten alınmış.";
                return View("register", model);
            }
            var checkmail =connection.QueryFirstOrDefault<User>("SELECT * FROM kullanicilar WHERE Email = @Email", new {model.Email});//email kontrol
            if (checkmail != null)
            {
                //Burada mevcut ise hata gösteriyorum
                ViewBag.ErrorMessage = "Bu Mail kullanılıyor.";
                return View("register", model);

            }
            model.CreatedDate = DateTime.Now;
            var sql = "INSERT INTO kullanicilar (UserName, Password, Email, CreatedDate, ImgPath) VALUES (@UserName, @Password, @Email, @CreatedDate, @ImgPath)";
            var data = new
            {
                model.UserName,
                Password = Helper.Hash(model.Password),
                model.Email,
                model.CreatedDate,
                model.ImgPath,

            };
            var rowsAffected = connection.Execute(sql, data);

            ViewBag.SuccessMessage = "Kayıt bilgileri mail adresinize gönderilmiştir";
            ViewBag.Message = "Kayıt başarılı";
            ViewBag.MessageCssClass = "alert-success";
            return View("Message");

        }

        public IActionResult Login(string? redirectUrl)
        {
            ViewBag.AuthError = TempData["AuthError"] as string;
            ViewBag.RedirectUrl = redirectUrl;
            return View(new User());
        }

        [HttpPost]
        [Route("/giris")]
        public IActionResult Giris(User model)
        {
            if (string.IsNullOrEmpty(model.UserName) || string.IsNullOrEmpty(model.Password))
            {
                TempData["AuthError"] = "Kullanıcı adı veya şifre boş olamaz";
                return RedirectToAction("login");
            }

            using var connection = new SqlConnection(connectionString);
            var sql = "SELECT * FROM kullanicilar WHERE UserName = @UserName AND Password = @Password";
            var user = connection.QuerySingleOrDefault<User>(sql, new { model.UserName, Password = Helper.Hash(model.Password) });

            if (user != null)
            {
                HttpContext.Session.SetInt32("Id", user.Id);
                HttpContext.Session.SetString("username", user.UserName);

                if (!string.IsNullOrEmpty(model.RedirectUrl))
                {
                    return Redirect(model.RedirectUrl);
                }
                return RedirectToAction("Index");
            }

            TempData["AuthError"] = "Kullanıcı adı veya şifre yanlış";
            return RedirectToAction("login");
        }


        public IActionResult Cikis()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("login");
        }

       

        public int? UserIdGetir(string username)
        {
            using var connection = new SqlConnection(connectionString);
            var sql = "SELECT Id FROM kullanicilar WHERE UserName = @username";
            var userId = connection.QueryFirstOrDefault<int?>(sql, new { UserName = username });
            return userId;
        }

        [Route("/profile/{username}")]
        public IActionResult Profile(string username)
        {
            ViewData["username"] = HttpContext.Session.GetString("username");
          int? userId = UserIdGetir(username);
            if (userId == null)// kullanıcı yoksa bunu gösteriyorm
            {
                ViewBag.MessageCssClass = "alert-alert";
                ViewBag.Message = "Böyle bir kullanıcı yok!.";
                return View("Message");
            }

            ViewBag.Post = true;
            if (!CheckLogin())
            {
                ViewBag.Post = false;
            }
            using var connection = new SqlConnection(connectionString);
            var postModel = new UserProfile();

            var sql = @"SELECT haber.Summary, haber.Id, kullanicilar.Username, haber.CreatedDate, kullanicilar.ImgPath, haber.ImgPath as TwtImg FROM haber LEFT JOIN kullanicilar on haber.UserId = kullanicilar.Id WHERE UserId = @UserId ORDER BY haber.CreatedDate DESC;
                       SELECT * FROM kullanicilar WHERE Id = @UserId";
            using var multi = connection.QueryMultiple(sql, new { userId });
            var post = multi.Read<Post>().ToList();
            var user = multi.ReadFirstOrDefault<User>();
            postModel.User = user;
            postModel.Posts = post;

            if (postModel.User.Id == HttpContext.Session.GetInt32("Id"))
            {
                ViewBag.yetki = "Full";
            }

            return View(postModel);
        }

        public IActionResult UserEdit(string? username)
        {
            ViewData["username"] = HttpContext.Session.GetString("username");

            if (!CheckLogin())
            {
                ViewBag.Message = "Bro?? Login ol!!";
                ViewBag.MessageCssClass = "alert-danger";
                return View("Message");
            }

            int? userId = UserIdGetir(username);
            if (userId == null || userId != HttpContext.Session.GetInt32("Id")) // kullanıcı yoksa ya da başka bir kullanıcı düzenlenmeye çalışıyorsa
            {
                ViewBag.MessageCssClass = "alert-danger";
                ViewBag.Message = "Böyle bir kullanıcı yok ya da yetkiniz yok!.";
                return View("Message");
            }

            using var connection = new SqlConnection(connectionString);
            var user = connection.QuerySingleOrDefault<User>("SELECT * FROM kullanicilar WHERE UserName = @UserName", new { UserName = username });


            return View(user);
        }

        [HttpPost]
        [Route("UserEdit/{id}")]
        public IActionResult UserName(User model)
        {
            if (string.IsNullOrEmpty(model.UserName))
            {
                return RedirectToAction("UserEdit", new { model.UserName });
            }
            using var connection = new SqlConnection(connectionString);

            var login = connection.QueryFirstOrDefault<User>("SELECT * FROM kullanicilar WHERE UserName = @UserName", new { model.UserName }); //kullanıcı adı kontrol
            if (login != null)
            {
                ViewBag.user = "Bu isim mevcut";
                return View("useredit",model);
            }


            var sql = "UPDATE kullanicilar SET UserName = @UserName WHERE Id = @Id";
            var data = new
            {
                model.UserName,
                model.Password,
                model.ImgPath,
                model.Id
            };
            var rowAffected = connection.Execute(sql, data);
            HttpContext.Session.SetString("username", model.UserName); // güncelleme sonrası session güncellemesi
            ViewData["username"] = HttpContext.Session.GetString("username");

            ViewBag.Message = "İsim güncellenmiştir.";
            ViewBag.MessageCssClass = "alert-success";
            return View("Message");
        }

            

        [HttpPost]
        [Route("passedit/{id}")]
        public IActionResult PassEdit(User model)
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                return RedirectToAction("UserEdit", new {model.UserName});
            }

            using var connection = new SqlConnection(connectionString);
            var sql = "UPDATE kullanicilar SET Password = @Password WHERE Id = @Id";
            model.Password = Helper.Hash(model.Password);

            var data = new
            {
                model.UserName,
                model.Password,
                model.ImgPath,
                model.Id
            };

            var rowAffected = connection.Execute(sql, data);

            ViewBag.Message = "Şifreniz güncellenmiştir.";
            ViewBag.MessageCssClass = "alert-success";
            return View("Message");
        }
        [HttpPost]
        [Route("FotoEdit/{id}")]
        public IActionResult FotoEdit(User model)
        {
            if (model.Img == null || model.Img.Length == 0)
            {
                return RedirectToAction("UserEdit", new { model.UserName });
            }
            using var connection = new SqlConnection(connectionString);
            var sql = "UPDATE kullanicilar SET ImgPath = @ImgPath WHERE Id = @Id";
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Img.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
            using var fileStream = new FileStream(filePath, FileMode.Create);
            model.Img.CopyTo(fileStream);
            model.ImgPath = fileName;

            var data = new
            {
                model.UserName,
                model.Password,
                model.ImgPath,
                model.Id
            };
            var rowAffected = connection.Execute(sql, data);
            ViewBag.Message = "Fotorafınız güncellenmiştir.";
            ViewBag.MessageCssClass = "alert-success";
            return View("Message");
        }


    }
}
