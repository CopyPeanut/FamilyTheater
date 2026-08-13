using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace LoginWindow.Models
{
    public class TagViewModel : ReactiveObject
    {
        public string Name { get; }

        [Reactive]
        public bool IsSelected { get; set; }

        public TagViewModel(string name)
        {
            Name = name;
        }
    }

    public class HomeWindowModel : ReactiveObject
    {
        private const int PageSize = 24;

        private readonly IUserService _userService;
        private readonly IMovieService _movieService;
        private readonly IPictureService _pictureService;
        private readonly Func<ConfigWindow> _configWindowFactory;
        private readonly Dictionary<string, ICategoryHandler> _categoryHandlers;

        public IMovieService MovieService => _movieService;

        public IPictureService PictureService => _pictureService;

        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public string JumpPageText { get; set; } = string.Empty;
        [Reactive] public string SearchText { get; set; } = string.Empty;
        [Reactive] public string ActiveCategory { get; set; } = "movie";

        public ObservableCollection<Movie> CurrentPageMovies { get; } = new();
        public ObservableCollection<Picture> CurrentPagePictures { get; } = new();
        public ObservableCollection<TagViewModel> Tags { get; } = new();
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

        public HomeWindowModel(
            IUserService userService,
            IMovieService movieService,
            IPictureService pictureService,
            Func<ConfigWindow> configWindowFactory)
        {
            _userService = userService;
            _movieService = movieService;
            _pictureService = pictureService;
            _configWindowFactory = configWindowFactory;

            _categoryHandlers = new Dictionary<string, ICategoryHandler>(StringComparer.OrdinalIgnoreCase)
            {
                ["movie"] = new CategoryHandler<Movie>(
                    owner: this,
                    loadItemsAsync: async () => await _movieService.GetAllMoviesAsync() ?? new List<Movie>(),
                    loadTagsAsync: _movieService.GetAllTagsAsync,
                    getSearchText: movie => movie.Title,
                    getItemTags: movie => movie.MovieTags?.Select(tag => tag.TagName) ?? Enumerable.Empty<string>(),
                    publishPageItems: ReplaceCurrentMovies),

                ["picture"] = new CategoryHandler<Picture>(
                    owner: this,
                    loadItemsAsync: async () => await _pictureService.GetAllPicturesAsync() ?? new List<Picture>(),
                    loadTagsAsync: _pictureService.GetAllTagsAsync,
                    getSearchText: picture => picture.FileName,
                    getItemTags: picture => picture.PictureTags?.Select(tag => tag.TagName) ?? Enumerable.Empty<string>(),
                    publishPageItems: ReplaceCurrentPictures)
            };

            var canMoveBack = this.WhenAnyValue(x => x.CurrentPage, page => page > 1);
            var canMoveForward = this.WhenAnyValue(x => x.CurrentPage, page => page < TotalPages);

            FirstPageCmd = ReactiveCommand.Create(() => CurrentPage = 1, canMoveBack);
            PrevPageCmd = ReactiveCommand.Create(() => CurrentPage--, canMoveBack);
            NextPageCmd = ReactiveCommand.Create(() => CurrentPage++, canMoveForward);
            LastPageCmd = ReactiveCommand.Create(() => CurrentPage = TotalPages, canMoveForward);
            GoToPageCmd = ReactiveCommand.Create<int>(page =>
            {
                if (page >= 1 && page <= TotalPages)
                {
                    CurrentPage = page;
                }
            });
            JumpPageCmd = ReactiveCommand.Create(() =>
            {
                if (int.TryParse(JumpPageText, out var page) && page >= 1 && page <= TotalPages)
                {
                    CurrentPage = page;
                }

                JumpPageText = string.Empty;
            });
            FillRandomCmd = ReactiveCommand.Create(FillRandomItems);
            OpenConfigCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                var window = _configWindowFactory();
                window.ShowDialog();
                await LoadCategoryAsync(ActiveCategory);
            });
            ToggleTagCmd = ReactiveCommand.Create<string>(tagName =>
            {
                var tag = Tags.FirstOrDefault(item => item.Name == tagName);
                if (tag == null)
                {
                    return;
                }

                tag.IsSelected = !tag.IsSelected;
                CurrentPage = 1;
                ApplyActiveCategoryFilter();
            });
            SwitchCategoryCmd = ReactiveCommand.CreateFromTask<string>(LoadCategoryAsync);

            this.WhenAnyValue(x => x.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    CurrentPage = 1;
                    ApplyActiveCategoryFilter();
                });

            this.WhenAnyValue(x => x.CurrentPage, x => x.TotalPages)
                .Subscribe(_ =>
                {
                    RefreshPageNumbers();
                    RefreshCurrentPageItems();
                });
        }

        public Task LoadMoviesAsync()
        {
            return LoadCategoryAsync("movie");
        }

        public Task LoadPicturesAsync()
        {
            return LoadCategoryAsync("picture");
        }

        private async Task LoadCategoryAsync(string category)
        {
            ActiveCategory = category;
            ResetFilters();

            if (TryGetActiveCategoryHandler(out var handler))
            {
                await handler.LoadAsync();
                return;
            }

            ClearCategoryPresentation();
        }

        private void ApplyActiveCategoryFilter()
        {
            if (TryGetActiveCategoryHandler(out var handler))
            {
                handler.ApplyFilter();
                return;
            }

            ClearCategoryPresentation();
        }

        private void RefreshCurrentPageItems()
        {
            if (TryGetActiveCategoryHandler(out var handler))
            {
                handler.RefreshCurrentPage();
                return;
            }

            ClearCurrentItems();
        }

        private void FillRandomItems()
        {
            if (TryGetActiveCategoryHandler(out var handler))
            {
                handler.FillRandomPage();
                return;
            }

            ClearCurrentItems();
        }

        private bool TryGetActiveCategoryHandler(out ICategoryHandler handler)
        {
            return _categoryHandlers.TryGetValue(ActiveCategory, out handler!);
        }

        private void ResetFilters()
        {
            SearchText = string.Empty;
            JumpPageText = string.Empty;
            CurrentPage = 1;

            foreach (var tag in Tags)
            {
                tag.IsSelected = false;
            }
        }

        private void ReplaceTags(IEnumerable<string> tagNames)
        {
            Tags.Clear();
            foreach (var name in tagNames)
            {
                Tags.Add(new TagViewModel(name));
            }
        }

        private void ReplaceCurrentMovies(IReadOnlyList<Movie> movies)
        {
            CurrentPagePictures.Clear();
            CurrentPageMovies.Clear();

            foreach (var movie in movies)
            {
                CurrentPageMovies.Add(movie);
            }
        }

        private void ReplaceCurrentPictures(IReadOnlyList<Picture> pictures)
        {
            CurrentPageMovies.Clear();
            CurrentPagePictures.Clear();

            foreach (var picture in pictures)
            {
                CurrentPagePictures.Add(picture);
            }
        }

        private HashSet<string> GetSelectedTagNames()
        {
            return Tags
                .Where(tag => tag.IsSelected)
                .Select(tag => tag.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private string GetNormalizedSearchText()
        {
            return (SearchText ?? string.Empty).Trim();
        }

        private void UpdatePagination(int itemCount)
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling(itemCount / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);
            RefreshPageNumbers();
            RefreshCurrentPageItems();
        }

        private void ClearCategoryPresentation()
        {
            ReplaceTags(Array.Empty<string>());
            TotalPages = 1;
            CurrentPage = 1;
            ClearCurrentItems();
            RefreshPageNumbers();
        }

        private void ClearCurrentItems()
        {
            CurrentPageMovies.Clear();
            CurrentPagePictures.Clear();
        }

        private void RefreshPageNumbers()
        {
            PageNumbers.Clear();

            for (var offset = -3; offset <= 3; offset++)
            {
                var page = CurrentPage + offset;
                PageNumbers.Add(page >= 1 && page <= TotalPages ? page : null);
            }
        }

        private interface ICategoryHandler
        {
            Task LoadAsync();

            void ApplyFilter();

            void RefreshCurrentPage();

            void FillRandomPage();
        }

        private sealed class CategoryHandler<TItem> : ICategoryHandler
        {
            private readonly HomeWindowModel _owner;
            private readonly Func<Task<List<TItem>>> _loadItemsAsync;
            private readonly Func<Task<List<string>>> _loadTagsAsync;
            private readonly Func<TItem, string?> _getSearchText;
            private readonly Func<TItem, IEnumerable<string>> _getItemTags;
            private readonly Action<IReadOnlyList<TItem>> _publishPageItems;

            private List<TItem> _allItems = new();
            private List<TItem> _filteredItems = new();

            public CategoryHandler(
                HomeWindowModel owner,
                Func<Task<List<TItem>>> loadItemsAsync,
                Func<Task<List<string>>> loadTagsAsync,
                Func<TItem, string?> getSearchText,
                Func<TItem, IEnumerable<string>> getItemTags,
                Action<IReadOnlyList<TItem>> publishPageItems)
            {
                _owner = owner;
                _loadItemsAsync = loadItemsAsync;
                _loadTagsAsync = loadTagsAsync;
                _getSearchText = getSearchText;
                _getItemTags = getItemTags;
                _publishPageItems = publishPageItems;
            }

            public async Task LoadAsync()
            {
                _allItems = await _loadItemsAsync();
                var tags = await _loadTagsAsync();
                _owner.ReplaceTags(tags);
                ApplyFilter();
            }

            public void ApplyFilter()
            {
                IEnumerable<TItem> query = _allItems;

                var selectedTagNames = _owner.GetSelectedTagNames();
                if (selectedTagNames.Count > 0)
                {
                    query = query.Where(item => _getItemTags(item).Any(selectedTagNames.Contains));
                }

                var searchText = _owner.GetNormalizedSearchText();
                if (!string.IsNullOrEmpty(searchText))
                {
                    query = query.Where(item =>
                        (_getSearchText(item) ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase));
                }

                _filteredItems = query.ToList();
                _owner.UpdatePagination(_filteredItems.Count);
            }

            public void RefreshCurrentPage()
            {
                var skip = (_owner.CurrentPage - 1) * PageSize;
                var currentPageItems = _filteredItems
                    .Skip(skip)
                    .Take(PageSize)
                    .ToList();

                _publishPageItems(currentPageItems);
            }

            public void FillRandomPage()
            {
                if (_filteredItems.Count == 0)
                {
                    _publishPageItems(Array.Empty<TItem>());
                    return;
                }

                var randomItems = _filteredItems
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(PageSize)
                    .ToList();

                _publishPageItems(randomItems);
            }
        }
    }
}
