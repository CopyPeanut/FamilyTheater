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
using System.Windows;

namespace LoginWindow.Models
{
    public class TagViewModel : ReactiveObject
    {
        public string Name { get; }

        [Reactive]
        public bool IsSelected { get; set; }

        [Reactive]
        public bool IsBatchSelected { get; set; }

        public TagViewModel(string name)
        {
            Name = name;
        }
    }

    public class BatchContentSelection
    {
        public string Category { get; init; } = string.Empty;

        public int Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;
    }

    public class BatchContentDeleteResult
    {
        public List<BatchContentSelection> Deleted { get; } = new();

        public List<string> Failures { get; } = new();
    }

    public class HomeWindowModel : ReactiveObject
    {
        private const int DefaultPageSize = 24;

        private readonly IUserService _userService;
        private readonly IMovieService _movieService;
        private readonly IPictureService _pictureService;
        private readonly IGameService _gameService;
        private readonly IMangaService _mangaService;
        private readonly ICurrentUserSession _currentUserSession;
        private readonly Func<ConfigWindow> _configWindowFactory;
        private readonly Func<UserPermissionsWindow> _userPermissionsWindowFactory;
        private readonly Func<ChangePasswordWindow> _changePasswordWindowFactory;
        private readonly Dictionary<string, ICategoryHandler> _categoryHandlers;
        private readonly Dictionary<int, BatchContentSelection> _batchSelectedMovies = new();
        private readonly Dictionary<int, BatchContentSelection> _batchSelectedPictures = new();
        private readonly Dictionary<int, BatchContentSelection> _batchSelectedGames = new();
        private readonly Dictionary<int, BatchContentSelection> _batchSelectedMangas = new();
        private ConfigWindow? _configWindow;
        private bool _isClosingDetachedWindows;

        public IMovieService MovieService => _movieService;

        public IPictureService PictureService => _pictureService;

        public IGameService GameService => _gameService;

        public IMangaService MangaService => _mangaService;

        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public string JumpPageText { get; set; } = string.Empty;
        [Reactive] public string SearchText { get; set; } = string.Empty;
        [Reactive] public string ActiveCategory { get; set; } = "movie";
        [Reactive] public int PageSize { get; private set; } = DefaultPageSize;
        [Reactive] public bool IsBatchDeleteMode { get; private set; }
        [Reactive] public int BatchSelectionVersion { get; private set; }
        public bool IsAdmin => _currentUserSession.IsAdmin;
        public string CurrentUserText => string.IsNullOrEmpty(_currentUserSession.Username)
            ? string.Empty
            : $"{_currentUserSession.Username} ({_currentUserSession.Role})";

        public ObservableCollection<Movie> CurrentPageMovies { get; } = new();
        public ObservableCollection<Picture> CurrentPagePictures { get; } = new();
        public ObservableCollection<Game> CurrentPageGames { get; } = new();
        public ObservableCollection<Manga> CurrentPageMangas { get; } = new();
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
            IMangaService mangaService,
            ICurrentUserSession currentUserSession,
            Func<ConfigWindow> configWindowFactory,
            Func<UserPermissionsWindow> userPermissionsWindowFactory,
            Func<ChangePasswordWindow> changePasswordWindowFactory)
        {
            _userService = userService;
            _movieService = movieService;
            _pictureService = pictureService;
            _gameService = gameService;
            _mangaService = mangaService;
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
                    publishPageItems: ReplaceCurrentGames),

                ["manga"] = new CategoryHandler<Manga>(
                    owner: this,
                    loadItemsAsync: async () => await _mangaService.GetAllMangasAsync() ?? new List<Manga>(),
                    loadTagsAsync: _mangaService.GetAllTagsAsync,
                    getSearchText: manga => manga.Title,
                    getItemTags: manga => manga.MangaTags?.Select(tag => tag.TagName) ?? Enumerable.Empty<string>(),
                    publishPageItems: ReplaceCurrentMangas)
            };

            var canMoveBack = this.WhenAnyValue(
                x => x.CurrentPage,
                x => x.TotalPages,
                (page, totalPages) => totalPages > 1 && page > 1);
            var canMoveForward = this.WhenAnyValue(
                x => x.CurrentPage,
                x => x.TotalPages,
                (page, totalPages) => page < totalPages);

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
            OpenConfigCmd = ReactiveCommand.Create(OpenConfigWindow);
            OpenUserPermissionsCmd = ReactiveCommand.Create(() =>
            {
                if (!_currentUserSession.IsAdmin)
                {
                    return;
                }

                var window = _userPermissionsWindowFactory();
                SetOwnerIfAvailable(window);
                window.ShowDialog();
            });
            OpenChangePasswordCmd = ReactiveCommand.Create(() =>
            {
                var window = _changePasswordWindowFactory();
                SetOwnerIfAvailable(window);
                window.ShowDialog();
            });
            LogoutCmd = ReactiveCommand.Create(() =>
            {
                _userService.Logout();
                LogoutRequested?.Invoke();
            });
            ToggleTagCmd = ReactiveCommand.Create<string>(tagName =>
            {
                if (IsBatchDeleteMode)
                {
                    ToggleBatchTag(tagName);
                    return;
                }

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

        public Task LoadMangasAsync()
        {
            return LoadCategoryAsync("manga");
        }

        private void OpenConfigWindow()
        {
            if (_configWindow != null)
            {
                RestoreAndActivate(_configWindow);
                return;
            }

            var window = _configWindowFactory();
            _configWindow = window;
            PositionNearOwner(window);
            window.Closed += ConfigWindow_Closed;
            window.Show();
            RestoreAndActivate(window);
        }

        private async void ConfigWindow_Closed(object? sender, EventArgs e)
        {
            if (sender is ConfigWindow window)
            {
                window.Closed -= ConfigWindow_Closed;
            }

            if (ReferenceEquals(_configWindow, sender))
            {
                _configWindow = null;
            }

            if (_isClosingDetachedWindows)
            {
                return;
            }

            await RefreshActiveCategoryAsync();
        }

        public void CloseDetachedWindows()
        {
            _isClosingDetachedWindows = true;
            try
            {
                if (_configWindow != null)
                {
                    _configWindow.Closed -= ConfigWindow_Closed;
                    _configWindow.Close();
                    _configWindow = null;
                }
            }
            finally
            {
                _isClosingDetachedWindows = false;
            }
        }

        private void SetOwnerIfAvailable(Window window)
        {
            var owner = FindOwnerWindow();
            if (owner != null && !ReferenceEquals(owner, window))
            {
                window.Owner = owner;
            }
        }

        private Window? FindOwnerWindow()
        {
            return System.Windows.Application.Current.Windows
                .OfType<HomeWindow>()
                .FirstOrDefault(window => ReferenceEquals(window.DataContext, this) && window.IsVisible);
        }

        private void PositionNearOwner(Window window)
        {
            var owner = FindOwnerWindow();
            if (owner == null)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            var ownerWidth = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
            var ownerHeight = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = owner.Left + Math.Max(0, (ownerWidth - window.Width) / 2);
            window.Top = owner.Top + Math.Max(0, (ownerHeight - window.Height) / 2);
        }

        private static void RestoreAndActivate(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
        }

        public async Task RefreshActiveCategoryAsync()
        {
            if (TryGetActiveCategoryHandler(out var handler))
            {
                var selectedTagNames = GetSelectedTagNames();
                await handler.LoadAsync(selectedTagNames);
                return;
            }

            ClearCategoryPresentation();
        }

        public async Task DeleteActiveTagAsync(string tagName, bool excludeFromScan = false)
        {
            var name = tagName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (ActiveCategory.Equals("movie", StringComparison.OrdinalIgnoreCase))
            {
                await _movieService.DeleteTagAsync(name, excludeFromScan);
            }
            else if (ActiveCategory.Equals("picture", StringComparison.OrdinalIgnoreCase))
            {
                await _pictureService.DeleteTagAsync(name, excludeFromScan);
            }
            else if (ActiveCategory.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                await _gameService.DeleteTagAsync(name, excludeFromScan);
            }
            else if (ActiveCategory.Equals("manga", StringComparison.OrdinalIgnoreCase))
            {
                await _mangaService.DeleteTagAsync(name, excludeFromScan);
            }

            await RefreshActiveCategoryAsync();
        }

        public async Task DeleteActiveTagsAsync(IEnumerable<string> tagNames, bool excludeFromScan = false)
        {
            foreach (var name in tagNames
                         .Select(tagName => tagName.Trim())
                         .Where(tagName => !string.IsNullOrEmpty(tagName))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (ActiveCategory.Equals("movie", StringComparison.OrdinalIgnoreCase))
                {
                    await _movieService.DeleteTagAsync(name, excludeFromScan);
                }
                else if (ActiveCategory.Equals("picture", StringComparison.OrdinalIgnoreCase))
                {
                    await _pictureService.DeleteTagAsync(name, excludeFromScan);
                }
                else if (ActiveCategory.Equals("game", StringComparison.OrdinalIgnoreCase))
                {
                    await _gameService.DeleteTagAsync(name, excludeFromScan);
                }
                else if (ActiveCategory.Equals("manga", StringComparison.OrdinalIgnoreCase))
                {
                    await _mangaService.DeleteTagAsync(name, excludeFromScan);
                }
            }

            await RefreshActiveCategoryAsync();
        }

        public void EnterBatchDeleteMode()
        {
            ClearBatchSelections();
            IsBatchDeleteMode = true;
        }

        public void ExitBatchDeleteMode()
        {
            IsBatchDeleteMode = false;
            ClearBatchSelections();
        }

        public void ToggleBatchTag(string tagName)
        {
            var tag = Tags.FirstOrDefault(item => item.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (tag == null)
            {
                return;
            }

            tag.IsBatchSelected = !tag.IsBatchSelected;
        }

        public IReadOnlyList<string> GetBatchSelectedTagNames()
        {
            return Tags
                .Where(tag => tag.IsBatchSelected)
                .Select(tag => tag.Name)
                .ToList();
        }

        public void ToggleBatchMovie(Movie movie)
        {
            ToggleBatchContent(_batchSelectedMovies, new BatchContentSelection
            {
                Category = "movie",
                Id = movie.Id,
                Title = movie.Title,
                FilePath = movie.VideoFilePath
            });
        }

        public void ToggleBatchPicture(Picture picture)
        {
            ToggleBatchContent(_batchSelectedPictures, new BatchContentSelection
            {
                Category = "picture",
                Id = picture.Id,
                Title = picture.FileName,
                FilePath = picture.FilePath
            });
        }

        public void ToggleBatchGame(Game game)
        {
            ToggleBatchContent(_batchSelectedGames, new BatchContentSelection
            {
                Category = "game",
                Id = game.Id,
                Title = game.Title,
                FilePath = game.FolderPath
            });
        }

        public void ToggleBatchManga(Manga manga)
        {
            ToggleBatchContent(_batchSelectedMangas, new BatchContentSelection
            {
                Category = "manga",
                Id = manga.Id,
                Title = manga.Title,
                FilePath = manga.FilePath
            });
        }

        public bool IsBatchContentSelected(string category, int id)
        {
            return GetBatchContentSelectionMap(category).ContainsKey(id);
        }

        public IReadOnlyList<BatchContentSelection> GetBatchSelectedContents()
        {
            return ActiveCategory.Equals("movie", StringComparison.OrdinalIgnoreCase)
                ? _batchSelectedMovies.Values.ToList()
                : ActiveCategory.Equals("picture", StringComparison.OrdinalIgnoreCase)
                    ? _batchSelectedPictures.Values.ToList()
                    : ActiveCategory.Equals("game", StringComparison.OrdinalIgnoreCase)
                        ? _batchSelectedGames.Values.ToList()
                        : ActiveCategory.Equals("manga", StringComparison.OrdinalIgnoreCase)
                            ? _batchSelectedMangas.Values.ToList()
                            : new List<BatchContentSelection>();
        }

        public async Task<BatchContentDeleteResult> DeleteBatchSelectedContentsAsync()
        {
            var result = new BatchContentDeleteResult();
            foreach (var item in GetBatchSelectedContents())
            {
                try
                {
                    if (item.Category.Equals("movie", StringComparison.OrdinalIgnoreCase))
                    {
                        await _movieService.DeleteMovieAsync(item.Id, deleteLocalFile: true);
                    }
                    else if (item.Category.Equals("picture", StringComparison.OrdinalIgnoreCase))
                    {
                        await _pictureService.DeletePictureAsync(item.Id, deleteLocalFile: true);
                    }
                    else if (item.Category.Equals("manga", StringComparison.OrdinalIgnoreCase))
                    {
                        await _mangaService.DeleteMangaAsync(item.Id, deleteLocalFile: true);
                    }
                    else
                    {
                        result.Failures.Add($"{item.Title}：当前分类不支持删除本地内容。");
                        continue;
                    }

                    result.Deleted.Add(item);
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{item.Title}\n{item.FilePath}\n{ex.Message}");
                }
            }

            await RefreshActiveCategoryAsync();
            return result;
        }

        public void UpdatePageSize(int pageSize)
        {
            var normalizedPageSize = Math.Max(1, pageSize);
            if (normalizedPageSize == PageSize)
            {
                return;
            }

            var firstVisibleItemIndex = Math.Max(0, (CurrentPage - 1) * PageSize);
            PageSize = normalizedPageSize;
            CurrentPage = firstVisibleItemIndex / PageSize + 1;
            ApplyActiveCategoryFilter();
        }

        private async Task LoadCategoryAsync(string category)
        {
            IsBatchDeleteMode = false;
            ClearBatchSelections();
            ActiveCategory = category;
            ResetFilters();

            if (TryGetActiveCategoryHandler(out var handler))
            {
                await handler.LoadAsync(selectedTagNames: null);
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

        private void ToggleBatchContent(Dictionary<int, BatchContentSelection> selectedItems, BatchContentSelection item)
        {
            if (!selectedItems.Remove(item.Id))
            {
                selectedItems[item.Id] = item;
            }

            BatchSelectionVersion++;
        }

        private Dictionary<int, BatchContentSelection> GetBatchContentSelectionMap(string category)
        {
            return category.Equals("movie", StringComparison.OrdinalIgnoreCase)
                ? _batchSelectedMovies
                : category.Equals("picture", StringComparison.OrdinalIgnoreCase)
                    ? _batchSelectedPictures
                    : category.Equals("game", StringComparison.OrdinalIgnoreCase)
                        ? _batchSelectedGames
                        : category.Equals("manga", StringComparison.OrdinalIgnoreCase)
                            ? _batchSelectedMangas
                            : new Dictionary<int, BatchContentSelection>();
        }

        private void ClearBatchSelections()
        {
            foreach (var tag in Tags)
            {
                tag.IsBatchSelected = false;
            }

            _batchSelectedMovies.Clear();
            _batchSelectedPictures.Clear();
            _batchSelectedGames.Clear();
            _batchSelectedMangas.Clear();
            BatchSelectionVersion++;
        }

        private void ReplaceTags(IEnumerable<string> tagNames, IReadOnlySet<string>? selectedTagNames = null)
        {
            Tags.Clear();
            foreach (var name in tagNames)
            {
                Tags.Add(new TagViewModel(name)
                {
                    IsSelected = selectedTagNames?.Contains(name) == true
                });
            }
        }

        private void ReplaceCurrentMovies(IReadOnlyList<Movie> movies)
        {
            CurrentPagePictures.Clear();
            CurrentPageGames.Clear();
            CurrentPageMangas.Clear();
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
            CurrentPageMangas.Clear();
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
            CurrentPageMangas.Clear();
            CurrentPageGames.Clear();

            foreach (var game in games)
            {
                CurrentPageGames.Add(game);
            }
        }

        private void ReplaceCurrentMangas(IReadOnlyList<Manga> mangas)
        {
            CurrentPageMovies.Clear();
            CurrentPagePictures.Clear();
            CurrentPageGames.Clear();
            CurrentPageMangas.Clear();

            foreach (var manga in mangas)
            {
                CurrentPageMangas.Add(manga);
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
            CurrentPageMangas.Clear();
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
            Task LoadAsync(IReadOnlySet<string>? selectedTagNames);

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

            public async Task LoadAsync(IReadOnlySet<string>? selectedTagNames)
            {
                _allItems = await _loadItemsAsync();
                var tags = await _loadTagsAsync();
                _owner.ReplaceTags(tags, selectedTagNames);
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
                var skip = (_owner.CurrentPage - 1) * _owner.PageSize;
                var currentPageItems = _filteredItems
                    .Skip(skip)
                    .Take(_owner.PageSize)
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
                    .Take(_owner.PageSize)
                    .ToList();

                _publishPageItems(randomItems);
            }
        }
    }
}
