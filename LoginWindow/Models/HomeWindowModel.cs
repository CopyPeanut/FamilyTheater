using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace LoginWindow.Models
{
    /// <summary>
    /// 标签 UI 包装：Name 显示名称，IsSelected 选中状态。
    /// </summary>
    public class TagViewModel : ReactiveObject
    {
        public string Name { get; }

        [Reactive] public bool IsSelected { get; set; }

        public TagViewModel(string name)
        {
            Name = name;
        }
    }

    public class HomeWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly IMovieService _movieService;
        private readonly IPictureService _pictureService;
        private readonly Func<ConfigWindow> _configWindowFactory;

        /// <summary>暴露给 View 层，用于打开详情弹窗时传递</summary>
        public IMovieService MovieService => _movieService;

        /// <summary>暴露给 View 层，用于打开图片详情弹窗时传递</summary>
        public IPictureService PictureService => _pictureService;

        /// <summary>每页显示的数量（电影和图片通用）</summary>
        private const int PageSize = 24;

        /// <summary>全部电影（从数据库加载，作为筛选源）</summary>
        private List<Movie> _allMovies = new();

        /// <summary>全部图片（从数据库加载，作为筛选源）</summary>
        private List<Picture> _allPictures = new();

        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public string JumpPageText { get; set; } = string.Empty;
        [Reactive] public string SearchText { get; set; } = string.Empty;

        /// <summary>当前激活的分类：movie / picture / manga / game</summary>
        [Reactive] public string ActiveCategory { get; set; } = "movie";

        /// <summary>当前页显示的电影（筛选+分页后的子集）</summary>
        public ObservableCollection<Movie> CurrentPageMovies { get; } = new();

        /// <summary>当前页显示的图片（筛选+分页后的子集）</summary>
        public ObservableCollection<Picture> CurrentPagePictures { get; } = new();

        /// <summary>全部标签（用于渲染标签方块）</summary>
        public ObservableCollection<TagViewModel> Tags { get; } = new();

        // 页码槽位，null 表示空位
        public ObservableCollection<int?> PageNumbers { get; } = new();

        public ReactiveCommand<Unit, int> FirstPageCmd { get; }
        public ReactiveCommand<Unit, int> PrevPageCmd { get; }
        public ReactiveCommand<Unit, int> NextPageCmd { get; }
        public ReactiveCommand<Unit, int> LastPageCmd { get; }
        public ReactiveCommand<int, Unit> GoToPageCmd { get; }
        public ReactiveCommand<Unit, Unit> JumpPageCmd { get; }
        public ReactiveCommand<Unit, Unit> FillRandomCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenConfigCmd { get; }
        public ReactiveCommand<string, Unit> ToggleTagCmd { get; }
        public ReactiveCommand<string, Unit> SwitchCategoryCmd { get; }

        public HomeWindowModel(IUserService userService, IMovieService movieService, IPictureService pictureService, Func<ConfigWindow> configWindowFactory)
        {
            _userService = userService;
            _movieService = movieService;
            _pictureService = pictureService;
            _configWindowFactory = configWindowFactory;

            var canMoveBack = this.WhenAnyValue(x => x.CurrentPage, p => p > 1);
            var canMoveForward = this.WhenAnyValue(x => x.CurrentPage, p => p < TotalPages);

            FirstPageCmd = ReactiveCommand.Create(() => CurrentPage = 1, canMoveBack);
            PrevPageCmd = ReactiveCommand.Create(() => CurrentPage--, canMoveBack);
            NextPageCmd = ReactiveCommand.Create(() => CurrentPage++, canMoveForward);
            LastPageCmd = ReactiveCommand.Create(() => CurrentPage = TotalPages, canMoveForward);
            GoToPageCmd = ReactiveCommand.Create<int>(page =>
            {
                if (page >= 1 && page <= TotalPages) CurrentPage = page;
            });
            JumpPageCmd = ReactiveCommand.Create(() =>
            {
                if (int.TryParse(JumpPageText, out var page) && page >= 1 && page <= TotalPages)
                    CurrentPage = page;
                JumpPageText = string.Empty;
            });
            FillRandomCmd = ReactiveCommand.Create(() =>
            {
                if (_allMovies.Count == 0) return;

                var random = new Random();
                var randomMovies = _allMovies
                    .OrderBy(_ => random.Next())
                    .Take(PageSize)
                    .ToList();

                CurrentPageMovies.Clear();
                foreach (var movie in randomMovies)
                    CurrentPageMovies.Add(movie);
            });
            OpenConfigCmd = ReactiveCommand.Create(() =>
            {
                var window = _configWindowFactory();
                window.ShowDialog();
                _ = LoadMoviesAsync();
            });

            // 标签点击：切换选中状态后重新筛选
            ToggleTagCmd = ReactiveCommand.Create<string>(tagName =>
            {
                var tag = Tags.FirstOrDefault(t => t.Name == tagName);
                if (tag != null)
                {
                    tag.IsSelected = !tag.IsSelected;
                    CurrentPage = 1;
                    ApplyFilter();
                }
            });

            // 分类切换：movie / picture / manga / game
            SwitchCategoryCmd = ReactiveCommand.Create<string>(category =>
            {
                ActiveCategory = category;
                // 清空搜索和标签选中状态
                SearchText = string.Empty;
                foreach (var tag in Tags)
                    tag.IsSelected = false;
                CurrentPage = 1;

                // 根据分类加载数据
                if (category == "picture")
                {
                    _ = LoadPicturesAsync();
                }
                else
                {
                    _ = LoadMoviesAsync();
                }
            });

            // 搜索文本变化时重新筛选（防抖 300ms）
            this.WhenAnyValue(x => x.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    CurrentPage = 1;
                    ApplyFilter();
                });

            // CurrentPage 或 TotalPages 变化时刷新页码和当前页电影列表
            this.WhenAnyValue(x => x.CurrentPage, x => x.TotalPages)
                .Subscribe(_ =>
                {
                    RefreshPageNumbers();
                    RefreshCurrentPageMovies();
                });
        }

        /// <summary>
        /// 从数据库加载全部电影和标签，填充首页。
        /// </summary>
        public async Task LoadMoviesAsync()
        {
            // 加载电影
            var movies = await _movieService.GetAllMoviesAsync();
            _allMovies = movies ?? new List<Movie>();

            // 加载标签并渲染为方块
            var tags = await _movieService.GetAllTagsAsync();
            Tags.Clear();
            foreach (var name in tags)
                Tags.Add(new TagViewModel(name));

            // 重置筛选并刷新
            SearchText = string.Empty;
            CurrentPage = 1;
            ApplyFilter();
        }

        /// <summary>
        /// 从数据库加载全部图片和标签，填充首页。
        /// </summary>
        public async Task LoadPicturesAsync()
        {
            // 加载图片
            var pictures = await _pictureService.GetAllPicturesAsync();
            _allPictures = pictures ?? new List<Picture>();

            // 加载图片标签并渲染为方块
            var tags = await _pictureService.GetAllTagsAsync();
            Tags.Clear();
            foreach (var name in tags)
                Tags.Add(new TagViewModel(name));

            // 重置筛选并刷新
            SearchText = string.Empty;
            CurrentPage = 1;
            ApplyFilter();
        }

        /// <summary>
        /// 按 SearchText + 选中的 Tags 筛选 _allMovies，重算 TotalPages 并刷新当前页。
        /// </summary>
        private void ApplyFilter()
        {
            if (ActiveCategory == "picture")
            {
                ApplyPictureFilter();
            }
            else
            {
                ApplyMovieFilter();
            }
        }

        private void ApplyMovieFilter()
        {
            IEnumerable<Movie> filtered = _allMovies;

            // 标签筛选：选中的标签取并集（电影包含任意一个选中标签即匹配）
            var selectedTagNames = Tags
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedTagNames.Count > 0)
            {
                filtered = filtered.Where(m =>
                    m.MovieTags != null &&
                    m.MovieTags.Any(mt =>
                        selectedTagNames.Contains(mt.TagName)));
            }

            // 搜索筛选：标题包含搜索文本（忽略大小写）
            var search = (SearchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(m =>
                    m.Title != null &&
                    m.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();

            TotalPages = Math.Max(1, (int)Math.Ceiling(filteredList.Count / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            // 存储筛选结果供分页使用
            _filteredMovies = filteredList;

            RefreshPageNumbers();
            RefreshCurrentPageMovies();
        }

        private void ApplyPictureFilter()
        {
            IEnumerable<Picture> filtered = _allPictures;

            // 标签筛选
            var selectedTagNames = Tags
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedTagNames.Count > 0)
            {
                filtered = filtered.Where(p =>
                    p.PictureTags != null &&
                    p.PictureTags.Any(pt =>
                        selectedTagNames.Contains(pt.TagName)));
            }

            // 搜索筛选：文件名包含搜索文本（忽略大小写）
            var search = (SearchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(p =>
                    p.FileName != null &&
                    p.FileName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();

            TotalPages = Math.Max(1, (int)Math.Ceiling(filteredList.Count / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            // 存储筛选结果供分页使用
            _filteredPictures = filteredList;

            RefreshPageNumbers();
            RefreshCurrentPageMovies();
        }

        private List<Movie> _filteredMovies = new();
        private List<Picture> _filteredPictures = new();

        private void RefreshCurrentPageMovies()
        {
            if (ActiveCategory == "picture")
            {
                CurrentPageMovies.Clear();
                CurrentPagePictures.Clear();
                var skip = (CurrentPage - 1) * PageSize;
                foreach (var picture in _filteredPictures.Skip(skip).Take(PageSize))
                    CurrentPagePictures.Add(picture);
            }
            else
            {
                CurrentPageMovies.Clear();
                CurrentPagePictures.Clear();
                var skip = (CurrentPage - 1) * PageSize;
                foreach (var movie in _filteredMovies.Skip(skip).Take(PageSize))
                    CurrentPageMovies.Add(movie);
            }
        }

        private void RefreshPageNumbers()
        {
            PageNumbers.Clear();

            for (int offset = -3; offset <= 3; offset++)
            {
                int page = CurrentPage + offset;
                if (page >= 1 && page <= TotalPages)
                    PageNumbers.Add(page);
                else
                    PageNumbers.Add(null);
            }
        }
    }
}
