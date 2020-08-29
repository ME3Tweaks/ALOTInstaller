﻿using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// From https://stackoverflow.com/questions/4716470/webclient-downloadstring-returns-string-with-peculiar-characters
    /// </summary>
    [Localizable(false)]
    public static class WebUtils
    {
        public static Encoding GetEncodingFrom(HttpContentHeaders responseHeaders,
            Encoding defaultEncoding = null)
        {
            if (responseHeaders == null)
                throw new ArgumentNullException(@"responseHeaders");

            //Note that key lookup is case-insensitive
            var charsetName = responseHeaders.ContentType.CharSet;
            if (charsetName == null)
                return defaultEncoding;

            //var contentTypeParts = contentType.Split(';');
            //if (contentTypeParts.Length <= 1)
            //    return defaultEncoding;

            //var charsetPart =
            //    contentTypeParts.Skip(1).FirstOrDefault(
            //        p => p.TrimStart().StartsWith(@"charset", StringComparison.InvariantCultureIgnoreCase));
            //if (charsetPart == null)
            //    return defaultEncoding;

            //var charsetPartParts = charsetPart.Split('=');
            //if (charsetPartParts.Length != 2)
            //    return defaultEncoding;

            //var charsetName = charsetPartParts[1].Trim();
            //if (charsetName == "")
            //    return defaultEncoding;

            try
            {
                return Encoding.GetEncoding(charsetName);
            }
            catch (ArgumentException ex)
            {
                throw new Exception(
                    @"The server returned data in an unknown encoding: " + charsetName,
                    ex);
            }
        }
    }
}