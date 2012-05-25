﻿using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Phone.Controls;
using Ocell.Library;
using Ocell.Library.Filtering;
using Ocell.Library.Twitter;
using TweetSharp;
using System.Threading;
using Ocell.Library.ReadLater.Instapaper;
using Ocell.Library.ReadLater.Pocket;
using Ocell.Library.ReadLater;
using System.Linq;

namespace Ocell.Commands
{
    public class ReplyCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return parameter is ITweetable;
        }

        public void Execute(object parameter)
        {
            ITweetable tweet = (ITweetable)parameter;
            DataTransfer.Text = "@" + tweet.Author.ScreenName + " ";
            if (parameter is TwitterStatus)
            {
                DataTransfer.ReplyId = tweet.Id;
                DataTransfer.ReplyingDM = false;
            }
            else if (parameter is TwitterDirectMessage)
            {
                DataTransfer.DMDestinationId = (parameter as TwitterDirectMessage).SenderId;
                DataTransfer.ReplyingDM = true;
            }

            PhoneApplicationFrame service = ((PhoneApplicationFrame)Application.Current.RootVisual);
            Deployment.Current.Dispatcher.BeginInvoke(() => service.Navigate(Uris.WriteTweet));
        }

        public event EventHandler CanExecuteChanged;
    }

    public class ReplyAllCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return parameter is TwitterStatus;
        }

        public void Execute(object parameter)
        {
            ITweetable tweet = (ITweetable)parameter;
            DataTransfer.ReplyId = tweet.Id;
            DataTransfer.Text = "";
            foreach (string user in StringManipulator.GetUserNames(tweet.Text))
                DataTransfer.Text += "@" + user + " ";
            PhoneApplicationFrame service = ((PhoneApplicationFrame)Application.Current.RootVisual);
            Deployment.Current.Dispatcher.BeginInvoke(() => service.Navigate(Uris.WriteTweet));
        }

        public event EventHandler CanExecuteChanged;
    }

    public class RetweetCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return (parameter is TwitterStatus) &&
                Config.Accounts.Count > 0 &&
                DataTransfer.CurrentAccount != null;
        }

        public void Execute(object parameter)
        {
            ServiceDispatcher.GetService(DataTransfer.CurrentAccount).Retweet(((ITweetable)parameter).Id, (sts, resp) => { Deployment.Current.Dispatcher.BeginInvoke(() => { MessageBox.Show("Retweeted!"); }); });
        }

        public event EventHandler CanExecuteChanged;
    }

    public class FavoriteCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return (parameter is TwitterStatus) &&
                Config.Accounts.Count > 0 &&
                DataTransfer.CurrentAccount != null;
        }

        public void Execute(object parameter)
        {
            ServiceDispatcher.GetService(DataTransfer.CurrentAccount).FavoriteTweet(((ITweetable)parameter).Id, (sts, resp) => { Deployment.Current.Dispatcher.BeginInvoke(() => { MessageBox.Show("Favorited!"); });  });
        }

        public event EventHandler CanExecuteChanged;
    }

    public class DeleteCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return (parameter is UserToken);
        }

        public void Execute(object parameter)
        {
            UserToken User = parameter as UserToken;
            if (User != null)
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBoxResult Result;
                    Result = MessageBox.Show("Are you sure you want to delete the account @" + User.ScreenName, "", MessageBoxButton.OKCancel);
                    if (Result == MessageBoxResult.OK)
                    {
                        Config.Accounts.Remove(User);
                        Config.SaveAccounts();
                        PhoneApplicationFrame service = ((PhoneApplicationFrame)Application.Current.RootVisual);
                        if (service.CanGoBack)
                            service.GoBack();
                    }
                });
        }

        public event EventHandler CanExecuteChanged;
    }

    public class ProtectCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return (parameter is UserToken);
        }

        public void Execute(object parameter)
        {
            try
            {
                ProtectedAccounts.SwitchAccountState(parameter as UserToken);
            }
            catch (Exception)
            {
            }
        }

        public event EventHandler CanExecuteChanged;
    }

    public class ModifyFilterCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return parameter is ITweetableFilter;
        }

        public void Execute(object parameter)
        {
            DataTransfer.Filter = parameter as ITweetableFilter;
            PhoneApplicationFrame service = ((PhoneApplicationFrame)Application.Current.RootVisual);
            service.Navigate(Uris.SingleFilter);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class RemoveFilterCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return parameter is ITweetableFilter;
        }

        public void Execute(object parameter)
        {
            DataTransfer.cFilter.RemoveFilter(parameter as ITweetableFilter);
            PhoneApplicationFrame service = ((PhoneApplicationFrame)Application.Current.RootVisual);
            service.Navigate(Uris.Filters);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class MuteCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return parameter is ITweetable;
        }

        public void Execute(object parameter)
        {
            ITweetable tweet = parameter as ITweetable;

            if (tweet == null)
                return;

            ITweeter author = tweet.Author;

            if (Config.GlobalFilter == null)
                Config.GlobalFilter = new ColumnFilter();

            ITweetableFilter filter = new ITweetableFilter();
            filter.Inclusion = IncludeOrExclude.Exclude;
            filter.Type = FilterType.User;
            filter.Filter = author.ScreenName;
            if (Config.DefaultMuteTime == TimeSpan.MaxValue)
                filter.IsValidUntil = DateTime.MaxValue;
            else
            filter.IsValidUntil = DateTime.Now + (TimeSpan)Config.DefaultMuteTime;

            Config.GlobalFilter.AddFilter(filter);
            Config.GlobalFilter = Config.GlobalFilter; // Force save.
            GlobalEvents.FireFiltersChanged(filter, new EventArgs());
        }

        public event EventHandler CanExecuteChanged;
    }

    public class ReadLaterCommand : ICommand
    {
        private int _pendingCalls;

        public bool CanExecute(object parameter)
        {
            var creds = Config.ReadLaterCredentials;
            return parameter is TwitterStatus && (creds.Instapaper != null || creds.Pocket != null);
        }

        public void Execute(object parameter)
        {
            TwitterStatus tweet = parameter as TwitterStatus;
            _pendingCalls = 0;
            var credentials = Config.ReadLaterCredentials;

            if (tweet == null)
                return;

            if (credentials.Pocket != null)
            {
                var service = new PocketService();
                service.UserName = credentials.Pocket.User;
                service.Password = credentials.Pocket.Password;

                TwitterUrl link = tweet.Entities.FirstOrDefault(item => item != null && item.EntityType == TwitterEntityType.Url) as TwitterUrl;
                _pendingCalls++;
                if (link != null)
                    service.AddUrl(link.ExpandedValue, tweet.Id, Callback);
                else
                {
                    string url = "http://twitter.com/" + tweet.Author.ScreenName + "/statuses/" + tweet.Id.ToString();
                    service.AddUrl(url, Callback);
                }
            }
            if (credentials.Instapaper != null)
            {
                var service = new InstapaperService();
                service.UserName = credentials.Instapaper.User;
                service.Password = credentials.Instapaper.Password;

                TwitterUrl link = tweet.Entities.FirstOrDefault(item => item != null && item.EntityType == TwitterEntityType.Url) as TwitterUrl;
                _pendingCalls++;
                if (link != null)
                    service.AddUrl(link.ExpandedValue, tweet.Text, Callback);
                else
                {
                    string url = "http://twitter.com/" + tweet.Author.ScreenName + "/statuses/" + tweet.Id.ToString();
                    service.AddUrl(url, Callback);
                }
            }
        }

        private void Callback(ReadLaterResponse response)
        {
            _pendingCalls--;
            if (response.Result != ReadLaterResult.Accepted)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() => { MessageBox.Show("There has been an error while trying to save this tweet for later."); });
            }
            else if (_pendingCalls <= 0)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() => { MessageBox.Show("Saved for later!"); });
            }
        }

        public event EventHandler CanExecuteChanged;
    }
}