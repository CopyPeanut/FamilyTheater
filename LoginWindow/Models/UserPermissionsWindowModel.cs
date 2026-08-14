using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace LoginWindow.Models
{
    public class UserPermissionItem : ReactiveObject
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsEditable { get; set; }
        public string[] RoleOptions { get; set; } = Array.Empty<string>();

        [Reactive] public string SelectedRole { get; set; } = UserRoles.User;
    }

    public class UserPermissionsWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly IAppLogger _logger;

        public ObservableCollection<UserPermissionItem> Users { get; } = new();

        [Reactive] public string StatusMessage { get; set; } = string.Empty;
        [Reactive] public bool IsSaving { get; set; }

        public ReactiveCommand<Unit, Unit> LoadCommand { get; }
        public ReactiveCommand<UserPermissionItem, Unit> SaveRoleCommand { get; }

        public UserPermissionsWindowModel(IUserService userService, IAppLogger logger)
        {
            _userService = userService;
            _logger = logger;

            LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
            SaveRoleCommand = ReactiveCommand.CreateFromTask<UserPermissionItem>(SaveRoleAsync);

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                Users.Clear();
                var users = await _userService.GetAllUsersAsync();

                foreach (var user in users)
                {
                    var normalizedRole = UserRoles.Normalize(user.Role);
                    var isEditable = UserRoles.IsEditableRole(normalizedRole);

                    Users.Add(new UserPermissionItem
                    {
                        Id = user.Id,
                        Username = user.Username,
                        SelectedRole = normalizedRole,
                        IsEditable = isEditable,
                        RoleOptions = isEditable
                            ? new[] { UserRoles.User, UserRoles.Manager }
                            : new[] { normalizedRole }
                    });
                }

                StatusMessage = users.Count == 0 ? "没有可管理的用户。" : string.Empty;
            }
            catch (Exception ex)
            {
                _logger.Error("加载用户权限列表失败。", ex);
                StatusMessage = $"加载失败：{ex.Message}";
            }
        }

        private async Task SaveRoleAsync(UserPermissionItem item)
        {
            if (item == null || !item.IsEditable)
            {
                return;
            }

            IsSaving = true;
            try
            {
                var success = await _userService.UpdateUserRoleAsync(item.Id, item.SelectedRole);
                StatusMessage = success
                    ? $"已更新 {item.Username} 的权限。"
                    : $"无法更新 {item.Username} 的权限。";
            }
            catch (Exception ex)
            {
                _logger.Error($"保存用户权限失败。UserId={item.Id}", ex);
                StatusMessage = $"保存失败：{ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
