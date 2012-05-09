﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Ocell.Library.Twitter;
using Ocell.Library;
using TweetSharp;

namespace Ocell.Pages.Lists
{
    public partial class ListManager : PhoneApplicationPage
    {
        private string _userName;
        private TweetSharp.TwitterService _srv;
        private bool _selectionChangeFired;
        private int _pendingCalls;
        public ListManager()
        {
            InitializeComponent();
            ThemeFunctions.ChangeBackgroundIfLightTheme(LayoutRoot);

            _selectionChangeFired = false;
            NewList.Click += new RoutedEventHandler(NewList_Click);
            _pendingCalls = 0;
            this.Loaded += new RoutedEventHandler(ListManager_Loaded);
        }

        void NewList_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(Uris.CreateList);
        }

        void ListManager_Loaded(object sender, RoutedEventArgs e)
        {
            if (!NavigationContext.QueryString.TryGetValue("user", out _userName))
            {
                NavigationService.GoBack();
                return;
            }

            _srv = ServiceDispatcher.GetService(DataTransfer.CurrentAccount);

            LoadListsIn();
            LoadUserLists();
        }

        private void LoadUserLists()
        {
            Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
            _pendingCalls++;
            _srv.ListListsFor(DataTransfer.CurrentAccount.ScreenName, -1, (lists, response) =>
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Dispatcher.BeginInvoke(() => MessageBox.Show("Error loading lists"));
                    return;
                }

                Dispatcher.BeginInvoke(() => ListsUser.ItemsSource = lists);
                _pendingCalls--;
                if(_pendingCalls <= 0)
                    Dispatcher.BeginInvoke(() => pBar.IsVisible = false);
            });
        }

        private void LoadListsIn()
        {
            Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
            _pendingCalls++;
            _srv.ListListMembershipsFor(_userName, true, -1, (lists, response) =>
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Dispatcher.BeginInvoke(() => MessageBox.Show("Error loading lists"));
                    return;
                }

                Dispatcher.BeginInvoke(() => ListsIn.ItemsSource = lists);
                _pendingCalls--;
                if (_pendingCalls <= 0)
                    Dispatcher.BeginInvoke(() => pBar.IsVisible = false);
            });
        }

        private void ListsIn_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_selectionChangeFired)
            {
                TwitterList list = null;
                if (e.AddedItems.Count > 0)
                    list = e.AddedItems[0] as TwitterList;
                if (list != null)
                {
                    Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
                    _srv.RemoveListMember(list.User.ScreenName, list.Slug, _userName, (user, response) =>
                    {
                        LoadListsIn();
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Dispatcher.BeginInvoke(() => MessageBox.Show("@" + _userName + " was removed from the list " + list.FullName + "."));
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(() => MessageBox.Show("An error occurred :("));
                        }
                        Dispatcher.BeginInvoke(() => pBar.IsVisible = false);
                    });
                }
                _selectionChangeFired = true;
                Dispatcher.BeginInvoke(() => ListsIn.SelectedIndex = -1);
            }
            else
            {
                _selectionChangeFired = false;
            }
        }

        private void ListsUser_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_selectionChangeFired)
            {
                TwitterList list = null;
                if (e.AddedItems.Count > 0)
                    list = e.AddedItems[0] as TwitterList;
                if (list != null)
                {
                    Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
                    _srv.AddListMember(list.User.ScreenName, list.Slug, _userName, (user, response) =>
                    {
                        LoadListsIn();
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Dispatcher.BeginInvoke(() => MessageBox.Show("@" + _userName + " was added to the list " + list.FullName + "."));
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(() => MessageBox.Show("An error occurred :("));
                        }
                        Dispatcher.BeginInvoke(() => pBar.IsVisible = false);
                    });
                }
                _selectionChangeFired = true;
                Dispatcher.BeginInvoke(() => ListsUser.SelectedIndex = -1);
            }
            else
            {
                _selectionChangeFired = false;
            }
        }
    }
}