using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class PostListViewModel
    {
        public List<ClientPostItemViewModel> Posts { get; set; } = new List<ClientPostItemViewModel>();
        public List<ClientPostCategoryViewModel> Categories { get; set; } = new List<ClientPostCategoryViewModel>();
        
        public string CurrentSearch { get; set; }
        public int? CurrentCategoryId { get; set; }
        
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class ClientPostItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public string Thumbnail { get; set; }
        public string CategoryName { get; set; }
        public string AuthorName { get; set; }
        public string PublishedDateStr { get; set; } // dd/MM/yyyy
    }

    public class ClientPostCategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int PostCount { get; set; }
        public bool IsSelected { get; set; }
    }

    public class PostDetailViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Thumbnail { get; set; }
        public string AuthorName { get; set; }
        public string AuthorAvatar { get; set; }
        public string PublishedDateStr { get; set; }
        public string CategoryName { get; set; }
        public int? CategoryId { get; set; }

        public List<ClientPostItemViewModel> RelatedPosts { get; set; } = new List<ClientPostItemViewModel>();
    }
}