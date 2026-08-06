using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;

namespace LoginWindow.Models
{
    public class HomeWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly Func<ConfigWindow> _configWindowFactory;
        [Reactive] public int CurrentPage { get; set; } = 1;
        [Reactive] public int TotalPages { get; set; } = 20; // TODO: 接真实数据时改
        [Reactive] public string JumpPageText { get; set; }

        // 改为 int?，null 表示预留空位
        public ObservableCollection<int?> PageNumbers { get; } = new();

        public ReactiveCommand<Unit, int> FirstPageCmd { get; }
        public ReactiveCommand<Unit, int> PrevPageCmd { get; }
        public ReactiveCommand<Unit, int> NextPageCmd { get; }
        public ReactiveCommand<Unit, int> LastPageCmd { get; }
        public ReactiveCommand<int, Unit> GoToPageCmd { get; }
        public ReactiveCommand<Unit, Unit> JumpPageCmd { get; }
        public ReactiveCommand<Unit, Unit> OpenConfigCmd { get; }

        public HomeWindowModel(IUserService userService, Func<ConfigWindow> configWindowFactory)
        {
            _userService = userService;
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
            });
            this.WhenAnyValue(x => x.CurrentPage, x => x.TotalPages)
                .Subscribe(_ => RefreshPageNumbers());
        }

        private void RefreshPageNumbers()
        {
            PageNumbers.Clear();

            // 固定 11 个槽位：当前页 ±5，当前页永远在索引 5（正中间）
            for (int offset = -3; offset <= 3; offset++)
            {
                int page = CurrentPage + offset;
                // 有效页码加入，超出范围的用 null 占位
                if (page >= 1 && page <= TotalPages)
                    PageNumbers.Add(page);
                else
                    PageNumbers.Add(null);
            }
        }
    }
}