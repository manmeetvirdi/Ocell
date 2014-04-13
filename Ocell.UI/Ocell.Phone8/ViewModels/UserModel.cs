﻿using AncoraMVVM.Base;
using Microsoft.Phone.Tasks;
using Ocell.Library;
using Ocell.Library.Twitter;
using Ocell.Localization;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using TweetSharp;

namespace Ocell.Pages.Elements
{
    [ImplementPropertyChanged]
    public class UserModel : ExtendedViewModelBase
    {
        public TwitterUser User { get; set; }

        public bool FriendshipRetrieved { get; set; }
        public bool Followed { get; set; }
        public bool FollowsMe { get; set; }
        public string RelationshipText { get; set; }
        public bool Blocked { get; set; }
        public bool IsOwner { get; set; }
        public string Avatar { get; set; }
        public string Name { get; set; }
        public string ScreenName { get; set; }
        public string Website { get; set; }
        public string Biography { get; set; }
        public string Tweets { get; set; }
        public string Following { get; set; }
        public string Followers { get; set; }
        public bool WebsiteEnabled { get; set; }

        #region Commands
        Func<object, bool> GenericCanExecute;

        DelegateCommand followUser;
        public ICommand FollowUser
        {
            get { return followUser; }
        }

        DelegateCommand unfollowUser;
        public ICommand UnfollowUser
        {
            get { return unfollowUser; }
        }

        DelegateCommand pinUser;
        public ICommand PinUser
        {
            get { return pinUser; }
        }

        DelegateCommand block;
        public ICommand Block
        {
            get { return block; }
        }

        DelegateCommand unblock;
        public ICommand Unblock
        {
            get { return unblock; }
        }

        DelegateCommand reportSpam;
        public ICommand ReportSpam
        {
            get { return reportSpam; }
        }

        DelegateCommand manageLists;
        public ICommand ManageLists
        {
            get { return manageLists; }
        }

        DelegateCommand changeAvatar;
        public ICommand ChangeAvatar
        {
            get { return changeAvatar; }
        }

        DelegateCommand navigateTo;
        public ICommand NavigateTo
        {
            get { return navigateTo; }
        }

        #endregion

        public UserModel()
        {
            User = null;
            GenericCanExecute = (obj) => User != null && DataTransfer.CurrentAccount != null;

            followUser = new DelegateCommand((obj) =>
            {
                Progress.IsLoading = true;
                ServiceDispatcher.GetService(DataTransfer.CurrentAccount).FollowUserAsync(new FollowUserOptions { UserId = User.Id }).ContinueWith(ReceiveFollow);
            }, x => FriendshipRetrieved && GenericCanExecute.Invoke(null));

            followUser.BindCanExecuteToProperty(this, "User", "Followed", "FriendshipRetrieved");

            unfollowUser = new DelegateCommand((obj) =>
            {
                Progress.IsLoading = true;
                ServiceDispatcher.GetService(DataTransfer.CurrentAccount).UnfollowUserAsync(new UnfollowUserOptions { UserId = User.Id }).ContinueWith(ReceiveFollow);
            }, x => FriendshipRetrieved && GenericCanExecute.Invoke(null));

            unfollowUser.BindCanExecuteToProperty(this, "User", "Followed", "FriendshipRetrieved");

            pinUser = new DelegateCommand((obj) =>
                {
                    Config.Columns.Value.Add(new TwitterResource
                    {
                        Data = User.ScreenName,
                        Type = ResourceType.Tweets,
                        User = DataTransfer.CurrentAccount
                    });
                    Config.SaveColumns();
                    Notificator.ShowProgressIndicatorMessage(Resources.UserPinned);
                    pinUser.RaiseCanExecuteChanged();

                }, item => GenericCanExecute.Invoke(null)
                    && !Config.Columns.Value.Any(o => o.Type == ResourceType.Tweets && o.Data == User.ScreenName));

            pinUser.BindCanExecuteToProperty(this, "User");

            block = new DelegateCommand((obj) =>
            {
                Progress.IsLoading = true;
                ServiceDispatcher.GetService(DataTransfer.CurrentAccount).BlockUserAsync(new BlockUserOptions { UserId = User.Id }).ContinueWith(ReceiveBlock);
            }, obj => GenericCanExecute(obj) && DataTransfer.CurrentAccount.ScreenName != User.ScreenName);

            block.BindCanExecuteToProperty(this, "Blocked", "User");

            unblock = new DelegateCommand((obj) =>
            {
                Progress.IsLoading = true;
                ServiceDispatcher.GetService(DataTransfer.CurrentAccount).UnblockUserAsync(new UnblockUserOptions { UserId = User.Id }).ContinueWith(ReceiveBlock);
            }, obj => GenericCanExecute(obj) && DataTransfer.CurrentAccount.ScreenName != User.ScreenName);

            unblock.BindCanExecuteToProperty(this, "Blocked", "User");

            reportSpam = new DelegateCommand(async (obj) =>
            {
                Progress.IsLoading = true;
                var response = await ServiceDispatcher.GetService(DataTransfer.CurrentAccount).ReportSpamAsync(new ReportSpamOptions { UserId = User.Id });
                Progress.IsLoading = false;

                if (response.StatusCode == HttpStatusCode.OK)
                    Notificator.ShowProgressIndicatorMessage(String.Format(Resources.ReportedAndBlocked, User.ScreenName));
                else
                    Notificator.ShowError(String.Format(Resources.CouldntReport, User.ScreenName));

            }, obj => GenericCanExecute(obj) && DataTransfer.CurrentAccount.ScreenName != User.ScreenName);

            reportSpam.BindCanExecuteToProperty(this, "User");

            changeAvatar = new DelegateCommand((obj) =>
            {
                PhotoChooserTask task = new PhotoChooserTask();
                task.ShowCamera = true;
                task.Completed += new EventHandler<PhotoResult>(PhotoSelected);
                task.Show();
            }, (obj) => GenericCanExecute.Invoke(null) && IsOwner);

            changeAvatar.BindCanExecuteToProperty(this, "User");

            navigateTo = new DelegateCommand((url) =>
            {
                var task = new WebBrowserTask();
                task.Uri = new Uri((string)url, UriKind.Absolute);
                task.Show();
            }, (url) => url is string && Uri.IsWellFormedUriString(url as string, UriKind.Absolute));

            navigateTo.BindCanExecuteToProperty(this, "Website", "WebsiteEnabled");

            manageLists = new DelegateCommand((obj) => Navigator.Navigate("/Pages/Lists/ListManager.xaml?user=" + User.ScreenName),
                GenericCanExecute);

            manageLists.BindCanExecuteToProperty(this, "User");

            this.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "FollowsMe")
                    UpdateRelationshipText();
                if (e.PropertyName == "ScreenName")
                    UpdateRelationshipText();
            };
        }

        private void UpdateRelationshipText()
        {
            if (!FriendshipRetrieved || IsOwner)
                RelationshipText = "";
            else if (FollowsMe)
                RelationshipText = String.Format(Resources.XFollowsY, ScreenName, DataTransfer.CurrentAccount.ScreenName);
            else
                RelationshipText = String.Format(Resources.XNotFollowsY, ScreenName, DataTransfer.CurrentAccount.ScreenName);
        }

        public override void OnLoad()
        {
            var userName = ReceiveMessage<string>();

            if (string.IsNullOrWhiteSpace(userName))
            {
                Notificator.ShowError(Resources.ErrorGettingProfile);
                Navigator.GoBack();
                return;
            }

            Regex remove = new Regex("@|:");
            userName = remove.Replace(userName, "");

            ScreenName = userName;

            GetUser(userName);
            base.OnLoad();
        }

        async void PhotoSelected(object sender, PhotoResult e)
        {
            UserToken usr;
            usr = Config.Accounts.Value.FirstOrDefault(item => item != null && item.ScreenName == User.ScreenName);
            if (e.TaskResult == TaskResult.OK && User != null)
            {
                Progress.Text = Resources.UploadingPicture;
                Progress.IsLoading = true;

                var response = await ServiceDispatcher.GetService(usr).UpdateProfileImageAsync(new UpdateProfileImageOptions { ImagePath = e.OriginalFileName });

                Progress.Text = "";
                Progress.IsLoading = false;
                if (response.StatusCode == HttpStatusCode.OK)
                    Notificator.ShowProgressIndicatorMessage(Resources.ProfileImageChanged);
                else
                    Notificator.ShowError(Resources.ErrorUploadingProfileImage);
            }
        }


        void ReceiveFollow(Task<TwitterResponse<TwitterUser>> task)
        {
            var response = task.Result;

            var usr = response.Content;

            string successMsg = "", errorMsg = "";

            if (usr == null)
            {
                Notificator.ShowError(Resources.ErrorMessage);
                return;
            }

            if (!Followed)
            {
                successMsg = String.Format(Resources.NowYouFollow, usr.ScreenName);
                errorMsg = String.Format(Resources.CouldntFollow, usr.ScreenName);
            }
            else
            {
                successMsg = String.Format(Resources.NowYouUnfollow, usr.ScreenName);
                errorMsg = String.Format(Resources.CouldntUnfollow, usr.ScreenName);
            }

            Progress.IsLoading = false;
            if (response.RequestSucceeded)
            {
                Notificator.ShowProgressIndicatorMessage(successMsg);
                Followed = !Followed;
            }
            else
                Notificator.ShowError(errorMsg);
        }

        void ReceiveBlock(Task<TwitterResponse<TwitterUser>> task)
        {
            var response = task.Result;

            string successMsg = "", errorMsg = "";

            if (!Blocked)
            {
                successMsg = String.Format(Resources.UserIsNowUnblocked, User.ScreenName);
                errorMsg = String.Format(Resources.CouldntUnblock, User.ScreenName);
            }
            else
            {
                successMsg = String.Format(Resources.UserIsNowBlocked, User.ScreenName);
                errorMsg = String.Format(Resources.CouldntBlock, User.ScreenName);
            }

            Progress.IsLoading = false;

            if (response.RequestSucceeded)
            {
                Notificator.ShowProgressIndicatorMessage(successMsg);
                Blocked = !Blocked;
            }
            else
                Notificator.ShowError(errorMsg);
        }

        async void GetUser(string userName)
        {
            Progress.Text = Resources.RetrievingUser;
            Progress.IsLoading = true;

            var response = await ServiceDispatcher.GetDefaultService().ListUserProfilesForAsync(new ListUserProfilesForOptions { ScreenName = new List<string> { userName } });
            var users = response.Content;
            Progress.Text = "";
            Progress.IsLoading = false;
            if (!response.RequestSucceeded || !users.Any())
            {
                Notificator.ShowError(Resources.CouldntFindUser);
                return;
            }

            User = users.First();

            if (User.ProfileImageUrl != null)
                Avatar = User.ProfileImageUrl.Replace("_normal", "");

            Name = User.Name;
            ScreenName = User.ScreenName;
            Website = User.Url;
            Biography = User.Description;
            Tweets = User.StatusesCount.ToString();
            Followers = User.FollowersCount.ToString();
            Following = User.FriendsCount.ToString();
            WebsiteEnabled = Uri.IsWellFormedUriString(User.Url, UriKind.Absolute);
            IsOwner = Config.Accounts.Value.Any(item => item.Id == User.Id);

            GetFriendshipInformation();
        }

        private void GetFriendshipInformation()
        {
            if (DataTransfer.CurrentAccount != null)
            {
                var service = ServiceDispatcher.GetService(DataTransfer.CurrentAccount);
                service.GetFriendshipInfoAsync(new GetFriendshipInfoOptions { SourceScreenName = DataTransfer.CurrentAccount.ScreenName, TargetScreenName = User.ScreenName }).ContinueWith(ReceiveFriendshipInfo);
                service.ListBlockedUserIdsAsync(new ListBlockedUserIdsOptions { Cursor = -1 }).ContinueWith(ReceiveBlockedUsers);
            }
        }

        void ReceiveFriendshipInfo(Task<TwitterResponse<TwitterFriendship>> task)
        {
            var response = task.Result;

            FriendshipRetrieved = true;
            if (!response.RequestSucceeded)
            {
                Notificator.ShowWarning(Resources.CouldntGetRelationship);
                return;
            }

            var friendship = response.Content;

            Followed = friendship.Relationship.Source.Following;
            FollowsMe = friendship.Relationship.Source.FollowedBy;
            UpdateRelationshipText();
            followUser.RaiseCanExecuteChanged();
            unfollowUser.RaiseCanExecuteChanged();
        }

        void ReceiveBlockedUsers(Task<TwitterResponse<TwitterCursorList<long>>> task)
        {
            IEnumerable<long> blockedIds = task.Result.Content;

            if (blockedIds == null)
                blockedIds = new List<long>();

            Blocked = blockedIds.Any() && blockedIds.Contains(User.Id);

            block.RaiseCanExecuteChanged();
            unblock.RaiseCanExecuteChanged();
        }
    }
}
