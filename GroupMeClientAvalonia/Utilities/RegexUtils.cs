﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GroupMeClientAvalonia.Utilities
{
    /// <summary>
    /// <see cref="RegexUtils"/> provides utility methods to help with Regular Expressions.
    /// </summary>
    public class RegexUtils
    {
        /// <summary>
        /// Regular Expression to match URLs in strings.
        /// </summary>
        public const string UrlRegex = @"(https?:\/\/(?:www\.|(?!www))[a-zA-Z0-9][a-zA-Z0-9-]+[a-zA-Z0-9]\.[^\s]{2,}|www\.[a-zA-Z0-9][a-zA-Z0-9-]+[a-zA-Z0-9]\.[^\s]{2,}|https?:\/\/(?:www\.|(?!www))[a-zA-Z0-9]+\.[^\s]{2,}|www\.[a-zA-Z0-9]+\.[^\s]{2,})"; // From https://stackoverflow.com/a/17773849
    }
}
