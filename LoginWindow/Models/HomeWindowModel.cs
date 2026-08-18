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
        private readonly IGameService _gameService;
        private readonly ICurrentUserSession _currentUserSession;
        private readonly Func<ConfigWindow> _configWindowFactory;
        private readonly Func<UserPermissionsWindow> _userPermissionsWindowFactory;
        private readonly Func<ChangePasswordWindow> _changePasswordWindowFactory;
        private readonly Dictionary<string, ICategoryHandler> _categoryHandlers;

        public IMovieService MovieService => _movieService;

        public IPictureService PictureService => _pictureService;

        public IGameService GameService => _gameService;

        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public string JumpPageText { get; set; } = string.Empty;
        [Reactive] public string SearchText { get; set; } = string.Empty;
        [Reactive] public string ActiveCategory { get; set; } = "movie";
        public bool IsAdmin => _currentUserSession.IsAdmin;
        public string CurrentUserText => string.IsNullOrEmpty(_currentUserSession.Username)
            ? string.Empty
            : $"{_currentUserSession.Username} ({_currentUserSession.Role})";

        public ObservableCollection<Movie> CurrentPageMovies { get; } = new();
        public ObservableCollection<Picture> CurrentPagePictures { get; } = new();
        public ObservableCollection<Game> CurrentPageGames { get; } = new();
        public ObservableCollection<TagViewModel> Tags { get; } = new();
        public ObservableCollection<int?> PageNumbers { get; } = new();

        public ReactiveCommand<Unit, int> FirstPageCmd { get; }
        public ReactiveCommand<Unit, int> PrevPageCmd { get; }
        public ReactiveCommand<Unit, int> NextPageCmd { get; }
        public ReactiveCommand<Unit, int> LastPageCmd { get; }
        public ReactiveCommand<int, Unit> GoToPageCmd { get; }
        public ReactiveCommand<Unit, Unit> JumpPageCmd { get; }
        public ReactiveCommand<Unit, Unit> FillRandomCmd { get; }
        public ReactiveCommand<Unit, Unit> SearchCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenConfigCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenUserPermissionsCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenChangePasswordCmd { get; }
        public ReactiveCommand<Unit, Unit> LogoutCmd { get; }
        public ReactiveCommand<string, Unit> ToggleTagCmd { get; }
        public ReactiveCommand<string, Unit> SwitchCategoryCmd { get; }

        public event Action? LogoutRequested;

        public HomeWindowModel(
            IUserService userService,
            IMovieService movieService,
            IPictureService pictureService,
            IGameService gameService,
            ICurrentUserSession currentUserSession,
            Func<ConfigWindow> configWindowFactory,
            Func<UserPermissionsWindow> userPermissionsWindowFactory,
            Func<ChangePasswordWindow> changePasswordWindowFactory)
        {
            _userService = userService;
            _movieService = movieService;
            _pictureService = pictureService;
            _gameService = gameService;
            _currentUserSession = currentUserSession;
            _configWindowFactory = configWindowFactory;
            _userPermissionsWindowFactory = userPermissionsWindowFactory;
            _changePasswordWindowFactory = changePasswordWindowFactory;

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
                    publishPageItems: ReplaceCurrentPictures),

                ["game"] = new CategoryHandler<Game>(
                    owner: this,
                    loadItemsAsync: async () => await _gameService.GetAllGamesAsync() ?? new List<Game>(),
                    loadTagsAsync: _gameService.GetAllTagsAsync,
                    getSearchText: game => game.Title,
                    getItemTags: game => game.GameTags?.Select(tag => tag.TagName) ?? Enumerable.Empty<string>(),
                    publishPageItems: ReplaceCurrentGames)
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
            SearchCmd = ReactiveCommand.Create(() =>
            {
                CurrentPage = 1;
                ApplyActiveCategoryFilter();
            });
            OpenConfigCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                var window = _configWindowFactory();
                window.ShowDialog();
                await LoadCategoryAsync(ActiveCategory);
            });
            OpenUserPermissionsCmd = ReactiveCommand.Create(() =>
            {
                if (!_currentUserSession.IsAdmin)
                {
                    return;
                }

                var window = _userPermissionsWindowFactory();
                window.ShowDialog();
            });
            OpenChangePasswordCmd = ReactiveCommand.Create(() =>
            {
                var window = _changePasswordWindowFactory();
                window.ShowDialog();
            });
            LogoutCmd = ReactiveCommand.Create(() =>
            {
                _userService.Logout();
                LogoutRequested?.Invoke();
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

        public Task LoadGamesAsync()
        {
            return LoadCategoryAsync("game");
        }

        public async Task DeleteActiveTagAsync(string tagName)
        {
            var name = tagName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (ActiveCategory.Equals("movie", StringComparison.OrdinalIgnoreCase))
            {
                await _movieService.DeleteTagAsync(name);
            }
            else if (ActiveCategory.Equals("picture", StringComparison.OrdinalIgnoreCase))
            {
                await _pictureService.DeleteTagAsync(name);
            }
            else if (ActiveCategory.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                await _gameService.DeleteTagAsync(name);
            }

            await LoadCategoryAsync(ActiveCategory);
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
            CurrentPageGames.Clear();
            CurrentPageMovies.Clear();

            foreach (var movie in movies)
            {
                CurrentPageMovies.Add(movie);
            }
        }

        private void ReplaceCurrentPictures(IReadOnlyList<Picture> pictures)
        {
            CurrentPageMovies.Clear();
            CurrentPageGames.Clear();
            CurrentPagePictures.Clear();

            foreach (var picture in pictures)
            {
                CurrentPagePictures.Add(picture);
            }
        }

        private void ReplaceCurrentGames(IReadOnlyList<Game> games)
        {
            CurrentPageMovies.Clear();
            CurrentPagePictures.Clear();
            CurrentPageGames.Clear();

            foreach (var game in games)
            {
                CurrentPageGames.Add(game);
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
            CurrentPageGames.Clear();
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
