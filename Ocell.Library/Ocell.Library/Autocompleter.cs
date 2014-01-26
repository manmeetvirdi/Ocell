﻿using AncoraMVVM.Base;
using PropertyChanged;
using System;
using System.Linq;

namespace Ocell.Library
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the number of characters between a position on the string and the first appearance of a character.
        /// </summary>
        /// <param name="str">String</param>
        /// <param name="end">Ending character</param>
        /// <param name="startIndex">Where to start searching</param>
        /// <returns>Length until the end character position or until the end of the string if end wasn't found.</returns>
        /// <example>"this is @example for me".LengthUntil(' ', 8) => 7</example>
        public static int LengthUntil(this string str, char end, int startIndex)
        {
            int endIndex = str.IndexOf(end, startIndex);

            if (endIndex == -1)
                return str.Length - startIndex - 1;
            else
                return endIndex - startIndex - 1;
        }

        public static bool Contains(this string str, char c)
        {
            foreach (var ch in str)
                if (c == ch)
                    return true;

            return false;
        }
    }

    [ImplementPropertyChanged]
    public class Autocompleter : ObservableObject
    {
        private IDataProvider<string> provider;
        private string previousText;
        private string written;
        int triggerPosition;

        public int SelectionStart { get; set; }
        public string InputText { get; set; }
        public char Trigger { get; set; }
        public bool IsAutocompleting { get; set; }
        public SafeObservable<string> Suggestions { get; protected set; }

        public Autocompleter(IDataProvider<string> provider)
        {
            this.provider = provider;

            if (provider != null)
                provider.StartRetrieval();

            Suggestions = new SafeObservable<string>();
            IsAutocompleting = false;
        }

        public void TextChanged(string text, int selectionStart)
        {
            InputText = text;
            this.SelectionStart = selectionStart;

            OnTextChanged();
        }

        private void OnTextChanged()
        {
            if (InputText == null)
                return;

            IsAutocompleting = GetAutocompletingState(InputText, previousText, SelectionStart);

            if (IsAutocompleting)
                UpdateAutocomplete();
        }

        internal bool GetAutocompletingState(string inputText, string previousText, int selectionStart)
        {
            if (inputText.Length > 0 && selectionStart > 0 && selectionStart <= inputText.Length && (Trigger != '\0' && inputText[selectionStart - 1] == Trigger))
                return true;

            if (selectionStart > 0 &&
                selectionStart < inputText.Length &&
                inputText[selectionStart - 1] == ' ' && previousText != null &&
                selectionStart < previousText.Length && previousText[selectionStart] != '@')
                return false;

            if (string.IsNullOrWhiteSpace(previousText))
                return false;

            if (!previousText.Contains(Trigger) && Trigger != '\0')
                return false;

            if (selectionStart <= previousText.Length && selectionStart > 0 && previousText[selectionStart - 1] == ' ')
                return false;

            int spaceIndex = triggerPosition < previousText.Length ? previousText.IndexOf(' ', triggerPosition) : -1;
            if (selectionStart <= triggerPosition || (spaceIndex != -1 && selectionStart > spaceIndex))
                return false;

            return false; // Return what?
        }

        private void UpdateAutocomplete()
        {
            // There's an strange reason which causes TextChanged to fire indefinitely, although the text has not changed really.
            // To avoid this, if the text we stored is the same, just return.
            if (previousText == InputText)
                return;

            previousText = InputText;

            written = GetTextWrittenByUser(InputText, previousText, SelectionStart, triggerPosition);

            Suggestions.Clear();

            foreach (var user in provider.DataList
                .Where(x => x.IndexOf(written, StringComparison.OrdinalIgnoreCase) != -1)
                .Take(20)
                .OrderBy(x => x))
            {
                Suggestions.Add(user);
            }
        }

        private void RemovePreviousAutocompleted()
        {
            if (string.IsNullOrWhiteSpace(previousText))
                return;

            int firstSpaceAfterSelStart = previousText.Substring(SelectionStart).IndexOf(' ');
            if (firstSpaceAfterSelStart == -1 && SelectionStart < previousText.Length)
                previousText = previousText.Remove(SelectionStart);
            else if (firstSpaceAfterSelStart != -1 && firstSpaceAfterSelStart + SelectionStart < previousText.Length &&
                firstSpaceAfterSelStart != 1)
                previousText = previousText.Remove(SelectionStart, firstSpaceAfterSelStart);
        }

        internal string GetTextWrittenByUser(string inputText, string previousText, int selectionStart, int triggerPosition)
        {
            if (selectionStart < previousText.Length)
                return previousText.Substring(triggerPosition + 1, selectionStart - triggerPosition - 1);
            else if (triggerPosition + 1 < previousText.Length)
                return previousText.Substring(triggerPosition + 1);
            else
                return "";
        }

        private string GetFirstUserCoincidentWith(string chunk)
        {
            return provider.DataList.FirstOrDefault(item =>
                item.IndexOf(chunk, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private void AutocompleteText(string text)
        {
            int insertPosition;
            if (SelectionStart > text.Length)
                insertPosition = text.Length;
            else
                insertPosition = SelectionStart;

            text = text.Insert(insertPosition, text);
        }

        private void UpdateTextbox()
        {
            int oldSelStart = SelectionStart;
            InputText = previousText;
            SelectionStart = oldSelStart;
        }

        internal string InsertSuggestionInText(string text, int triggerPosition, string toInsert)
        {
            // Remove the user text written until now.
            var nextSpace = text.IndexOf(' ', triggerPosition);

            var newText = text.Substring(0, triggerPosition + 1) + toInsert;

            if (nextSpace != -1)
                newText += text.Substring(nextSpace);

            return newText;
        }

        public void UserChoseElement(string name)
        {
            IsAutocompleting = false;
            written = "";

            var newText = InsertSuggestionInText(previousText, triggerPosition, name);

            previousText = newText;
            InputText = previousText;
        }
    }
}
