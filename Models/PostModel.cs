using System.ComponentModel.DataAnnotations;

namespace XClone.Models
{
    public class Post
    {
        public int Id { get; set; }
        [Required]
        public string Summary { get; set; }
        public IFormFile? Img { get; set; }
        public string? ImgPath { get; set; }
        public string Slug { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string TwtImg { get; set; }

    }

    public class User
    {
        public int Id { get; set; }
        [Required]
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PasswordRepeat { get; set; }
        public IFormFile? Img { get; set; }
        public string? ImgPath { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? RedirectUrl { get; set; }

    }

    public class PostCommet
    {
        public int Id { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedDate { get; set; }
        public int UserId { get; set; }
        public int PostId { get; set; }
        public string? UserName { get; set; }
        public string? ImgPath { get; set; }
    }

    public class PostModel
    {
        public Post Post { get; set; }
        public List<PostCommet> Comments { get; set; }
    }
     public class UserProfile
    {
        public User? User { get; set; }
        public List<Post> Posts { get; set; }
    }


}
