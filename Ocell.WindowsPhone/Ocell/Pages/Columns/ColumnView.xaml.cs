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

namespace Ocell.Pages.Columns
{
    public partial class ColumnView : PhoneApplicationPage
    {
        public ColumnView()
        {
            InitializeComponent(); 
            Loaded += (sender, e) => { if (ApplicationBar != null) ApplicationBar.MatchOverriddenTheme(); };
            ThemeFunctions.SetBackground(LayoutRoot);

            DataContext = new ColumnViewModel();
        }
    }
}