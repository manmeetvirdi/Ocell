﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.IO.IsolatedStorage;
using System.Text;

namespace Ocell.Library
{
    public static class SchedulerSync
    {
        private const string FileName = "DateFile";

        public static void WriteLastCheckDate(DateTime Date)
        {
            IsolatedStorageFile Storage = IsolatedStorageFile.GetUserStoreForApplication();
            IsolatedStorageFileStream File = Storage.OpenFile(FileName, System.IO.FileMode.Create);
            UTF8Encoding Encoding = new UTF8Encoding();
            string DateStr;
            byte[] bytes;

            DateStr = Date.ToString("s");
            bytes = Encoding.GetBytes(DateStr);

            File.Write(bytes, 0, bytes.Length);
            File.Close();
        }

        public static DateTime GetLastCheckDate()
        {
            IsolatedStorageFile Storage = IsolatedStorageFile.GetUserStoreForApplication();
            IsolatedStorageFileStream File = Storage.OpenFile(FileName, System.IO.FileMode.OpenOrCreate);
            UTF8Encoding Encoding = new UTF8Encoding();
            string DateStr;
            byte[] bytes = new byte[File.Length];
            DateTime Date;

            File.Read(bytes, 0, (int)File.Length);
            DateStr = new string(Encoding.GetChars(bytes));

            if (!DateTime.TryParse(DateStr, out Date))
                Date = DateTime.Now;

            return Date;
        }
    }
}
