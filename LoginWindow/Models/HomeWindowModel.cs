using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace LoginWindow.Models
{
    public class HomeWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly IMovieService _movieService;
        private readonly Func<ConfigWindow> _configWindowFactory;

        /// <summary>每页显示的电影数量</summary>
        private const int PageSize = 24;

        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public string JumpPageText { get; set; } = string.Empty;

        /// <summary>全部电影（从数据库加载）</summary>
        public ObservableCollection<Movie> AllMovies { get; } = new();

        /// <summary>当前页显示的电影（分页后的子集）</summary>
        public ObservableCollection<Movie> CurrentPageMovies { get; } = new();

        // 改为 int?，null 表示预留空位
        public ObservableCollection<int?> PageNumbers { get; } = new();

        public ReactiveCommand<Unit, int> FirstPageCmd { get; }
        public ReactiveCommand<Unit, int> PrevPageCmd { get; }
        public ReactiveCommand<Unit, int> NextPageCmd { get; }
        public ReactiveCommand<Unit, int> LastPageCmd { get; }
        public ReactiveCommand<int, Unit> GoToPageCmd { get; }
        public ReactiveCommand<Unit, Unit> JumpPageCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenConfigCmd { get; }

        public HomeWindowModel(IUserService userService, IMovieService movieService, Func<ConfigWindow> configWindowFactory)
        {
            _userService = userService;
            _movieService = movieService;
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
            OpenConfigCmd = ReactiveCommand.Create(() =>
            {
                var window = _configWindowFactory();
                window.ShowDialog();
                // Config 关闭后重新加载数据库中的电影（扫描入库后刷新海报列表）
                _ = LoadMoviesAsync();
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
        /// 从数据库加载全部电影，计算总页数，填充首页。
        /// </summary>
        public async Task LoadMoviesAsync()
        {
            var movies = await _movieService.GetAllMoviesAsync();
            AllMovies.Clear();
            foreach (var m in movies)
                AllMovies.Add(m);

            TotalPages = Math.Max(1, (int)Math.Ceiling(AllMovies.Count / (double)PageSize));
            CurrentPage = 1;

            // 手动刷新，不依赖 CurrentPage 变更通知（CurrentPage 可能本来就是 1，不会触发 Subscribe）
            RefreshPageNumbers();
            RefreshCurrentPageMovies();
        }

        private void RefreshCurrentPageMovies()
        {
            CurrentPageMovies.Clear();
            var skip = (CurrentPage - 1) * PageSize;
            foreach (var movie in AllMovies.Skip(skip).Take(PageSize))
                CurrentPageMovies.Add(movie);
        }

        private void RefreshPageNumbers()
        {
            PageNumbers.Clear();

            // 固定 7 个槽位：当前页 ±3
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
