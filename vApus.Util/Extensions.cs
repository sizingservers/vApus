﻿/*
 * Copyright 2009 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace vApus.Util
{
    public static class AssemblyExtension
    {
        /// <summary>
        ///     Gets a type by its type name.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="typeName"></param>
        /// <returns>The type if found, otherwise null.</returns>
        public static Type GetTypeByName(this Assembly assembly, string typeName)
        {
            foreach (Type t in assembly.GetTypes())
                if (t.Name == typeName)
                    return t;
            return null;
        }
    }

    public static class TypeExtension
    {
        /// <summary>
        ///     To check if a type has an indirect base type (given)
        /// </summary>
        /// <param name="t"></param>
        /// <param name="BaseType"></param>
        /// <returns></returns>
        public static bool HasBaseType(this Type t, Type BaseType)
        {
            Type type = t;
            while (type.BaseType != null)
            {
                if (type.BaseType == BaseType)
                    return true;
                type = type.BaseType;
            }
            return false;
        }
    }

    public static class TimeSpanExtension
    {
        public static string ToLongFormattedString(this TimeSpan timeSpan)
        {
            var sb = new StringBuilder();
            if (timeSpan.Days != 0)
            {
                sb.Append(timeSpan.Days);
                sb.Append(" days");
            }
            if (timeSpan.Hours != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Hours);
                sb.Append(" hours");
            }
            if (timeSpan.Minutes != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Minutes);
                sb.Append(" minutes");
            }
            if (timeSpan.Seconds != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Seconds);
                sb.Append(" seconds");
            }
            if (timeSpan.Milliseconds != 0 || sb.ToString().Length == 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Milliseconds);
                sb.Append(" milliseconds");
            }
            return sb.ToString();
        }

        public static string ToShortFormattedString(this TimeSpan timeSpan)
        {
            var sb = new StringBuilder();
            if (timeSpan.Days != 0)
            {
                sb.Append(timeSpan.Days);
                sb.Append(" d");
            }
            if (timeSpan.Hours != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Hours);
                sb.Append(" h");
            }
            if (timeSpan.Minutes != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Minutes);
                sb.Append(" m");
            }
            if (timeSpan.Seconds != 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Seconds);
                sb.Append(" s");
            }
            if (timeSpan.Milliseconds != 0 || sb.ToString().Length == 0)
            {
                if (sb.ToString().Length != 0)
                    sb.Append(", ");
                sb.Append(timeSpan.Milliseconds);
                sb.Append(" ms");
            }
            return sb.ToString();
        }
    }

    public static class StringExtension
    {
        public static bool ContainsChars(this string s, params char[] values)
        {
            foreach (char value in values)
                if (!s.Contains(value))
                    return false;
            return true;
        }

        public static string RemoveChars(this string s, params char[] values)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                if (!values.Contains(c))
                    sb.Append(c);
            return sb.ToString();
        }

        public static bool ContainsStrings(this string s, params string[] values)
        {
            foreach (string value in values)
                if (!s.Contains(value))
                    return false;
            return true;
        }

        /// <summary>
        ///     Determines if the string does or does not contain \,*,/,:,<,>,?,\ or |.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsValidWindowsFilenameString(this string s)
        {
            if (s == null || s.Length == 0)
                return false;
            foreach (char c in s)
                if (!c.IsValidWindowsFilenameChar())
                    return false;
            return true;
        }

        /// <summary>
        ///     Replaces \,*,/,:,<,>,?,\ and | with the given character in a new string.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="newChar"></param>
        /// <returns></returns>
        public static string ReplaceInvalidWindowsFilenameChars(this string s, char newChar)
        {
            var sb = new StringBuilder(s.Length);
            if (s == null)
                throw new ArgumentNullException("s");
            foreach (char c in s)
                if (c.IsValidWindowsFilenameChar())
                    sb.Append(c);
                else
                    sb.Append(newChar);
            return sb.ToString();
        }

        /// <summary>
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsNumeric(this string s)
        {
            ulong ul;
            double d;
            return ulong.TryParse(s, out ul) || double.TryParse(s, out d);
        }

        /// <summary>
        ///     A simple way to encrypt a string.
        ///     Example (don't use this): s.Encrypt("password", new byte[] { 0x59, 0x06, 0x59, 0x3e, 0x21, 0x4e, 0x55, 0x34, 0x96, 0x15, 0x11, 0x13, 0x72 });
        /// </summary>
        /// <param name="s"></param>
        /// <param name="password"></param>
        /// <param name="salt"></param>
        /// <returns>The encrypted string.</returns>
        public static string Encrypt(this string s, string password, byte[] salt)
        {
            var pdb = new PasswordDeriveBytes(password, salt);
            byte[] encrypted = Encrypt(System.Text.Encoding.Unicode.GetBytes(s), pdb.GetBytes(32), pdb.GetBytes(16));
            return Convert.ToBase64String(encrypted);
        }

        private static byte[] Encrypt(byte[] toEncrypt, byte[] key, byte[] IV)
        {
            var ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();
            alg.Key = key;
            alg.IV = IV;
            var cs = new CryptoStream(ms, alg.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(toEncrypt, 0, toEncrypt.Length);
            cs.Close();
            return ms.ToArray();
        }

        /// <summary>
        ///     A simple way to decrypt a string.
        ///     Example (don't use this): s.Decrypt("password", new byte[] { 0x59, 0x06, 0x59, 0x3e, 0x21, 0x4e, 0x55, 0x34, 0x96, 0x15, 0x11, 0x13, 0x72 });
        /// </summary>
        /// <param name="s"></param>
        /// <param name="password"></param>
        /// <param name="salt"></param>
        /// <returns>The decrypted string.</returns>
        public static string Decrypt(this string s, string password, byte[] salt)
        {
            var pdb = new PasswordDeriveBytes(password, salt);
            byte[] decrypted = Decrypt(Convert.FromBase64String(s), pdb.GetBytes(32), pdb.GetBytes(16));
            return System.Text.Encoding.Unicode.GetString(decrypted);
        }

        private static byte[] Decrypt(byte[] toDecrypt, byte[] Key, byte[] IV)
        {
            var ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();
            alg.Key = Key;
            alg.IV = IV;
            var cs = new CryptoStream(ms, alg.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(toDecrypt, 0, toDecrypt.Length);
            try
            {
                cs.Close();
            }
            catch
            {
            }
            return ms.ToArray();
        }

        public static ListViewItem Parse(this string s, char delimiter)
        {
            var item = new ListViewItem();
            string[] split = s.Split(delimiter);
            item.Text = split[0];
            for (int i = 1; i < split.Length; i++)
                item.SubItems.Add(split[i]);
            return item;
        }

        public static string Reverse(this string s)
        {
            var sb = new StringBuilder(s.Length);
            ;
            for (int i = s.Length - 1; i != -1; i--)
                sb.Append(s[i]);

            return sb.ToString();
        }

        public static object ToByteArrayToObject(this string s, string separator = ",")
        {
            string[] split = s.Split(new[] {separator}, StringSplitOptions.None);
            var buffer = new byte[split.Length];
            for (int i = 0; i != split.Length; i++)
                buffer[i] = byte.Parse(split[i]);

            object o = null;
            using (var ms = new MemoryStream(buffer))
            {
                var bf = new BinaryFormatter();
                o = bf.UnsafeDeserialize(ms, null);
                bf = null;
            }
            buffer = null;

            return o;
        }
    }

    public static class CharExtension
    {
        /// <summary>
        ///     Determines if the char is or is not \,*,/,:,<,>,?,\ or |.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsValidWindowsFilenameChar(this char c)
        {
            switch ((int) c)
            {
                case 34: // '\"'
                case 42: // '*'
                case 47: // '/'
                case 58: // ':'
                case 60: // '<'
                case 62: // '>'
                case 63: // '?'
                case 92: // '\\'
                case 124: // '|'
                    return false;
            }
            return true;
        }

        /// <summary></summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsLetter(this char c)
        {
            int i = c;
            return ((i > 64 && i < 91) || (i > 96 && i < 123));
        }

        /// <summary></summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsDigit(this char c)
        {
            int i = c;
            return (i > 47 && i < 58);
        }
    }

    public static class ObjectExtension
    {
        public delegate void ParentChangedEventHandler(ParentOrTagChangedEventArgs parentOrTagChangedEventArgs);

        public delegate void TagChangedEventHandler(ParentOrTagChangedEventArgs parentOrTagChangedEventArgs);

        private static readonly object _lock = new object();
        //Nifty hack to make this work everywhere (also in derived types when shallow copying).
        //Having just a static field for tag and parent doesn't work, they will be the same for every object you assign them.
        //Do not use this for primary datatypes (strings included) except if you do something like this:
        //Object o = 1;
        [NonSerialized] private static readonly Hashtable _tags = new Hashtable();
        [NonSerialized] private static readonly Hashtable _parents = new Hashtable();
        [NonSerialized] private static readonly Hashtable _descriptions = new Hashtable();
        public static event ParentChangedEventHandler ParentChanged;

        /// <summary>
        ///     Nifty hack to make this work everywhere (also in derived types when shallow copying).
        ///     Having just a static field for tag and parent doesn't work, they will be the same for every object you assign them.
        ///     Do not use this for primary datatypes (strings included) except if you do something like this:
        ///     Object o = 1;
        /// </summary>
        /// <param name="o"></param>
        /// <param name="tag"></param>
        public static void SetTag(this object o, object tag)
        {
            lock (_tags.SyncRoot)
                if (o != null)
                {
                    if (_tags.Contains(o))
                    {
                        if (tag == null)
                            _tags.Remove(o);
                        else
                            _tags[o] = tag;
                    }
                    else if (tag != null)
                    {
                        _tags.Add(o, tag);
                    }
                }
        }

        public static object GetTag(this object o)
        {
            //Threadsafe for reader threads.
            if (o == null)
                return null;
            return _tags.Contains(o) ? _tags[o] : null;
        }

        /// <summary>
        ///     Nifty hack to make this work everywhere (also in derived types when shallow copying).
        ///     Having just a static field for tag and parent doesn't work, they will be the same for every object you assign them.
        ///     Do not use this for primary datatypes (strings included) except if you do something like this:
        ///     Object o = 1;
        /// </summary>
        /// <param name="o"></param>
        /// <param name="parent"></param>
        public static void SetParent(this object o, object parent, bool invokeParentChanged = true)
        {
            lock (_parents.SyncRoot)
                if (o != null)
                {
                    object previous = null;

                    if (_parents.Contains(o))
                    {
                        previous = _parents[o];
                        if (parent == null)
                        {
                            _parents.Remove(o);
                        }
                        else
                        {
                            if (previous != null && !previous.Equals(parent))
                                _parents[o] = parent;
                            else
                                return;
                        }
                    }
                    else
                    {
                        if (parent == null)
                            return;
                        _parents.Add(o, parent);
                    }

                    if (invokeParentChanged && ParentChanged != null)
                        foreach (ParentChangedEventHandler del in ParentChanged.GetInvocationList())
                            del.BeginInvoke(new ParentOrTagChangedEventArgs(o, previous, parent), null, null);
                }
        }

        public static object GetParent(this object o)
        {
            //Threadsafe for reader threads.
            if (o == null)
                return null;
            return _parents.Contains(o) ? _parents[o] : null;
        }

        public static void SetDescription(this object o, string description)
        {
            lock (_descriptions.SyncRoot)
                if (o != null)
                    if (_descriptions.Contains(o))
                    {
                        if (description == null)
                            _descriptions.Remove(o);
                        else
                            _descriptions[o] = description;
                    }
                    else if (description != null)
                    {
                        _descriptions.Add(o, description);
                    }
        }

        public static string GetDescription(this object o)
        {
            //Threadsafe for reader threads.
            if (o == null)
                return null;
            return (_descriptions.Contains(o) ? _descriptions[o] : null) as string;
        }

        /// <summary>
        /// </summary>
        /// <param name="o">Child</param>
        /// <returns>True if the object was removed.</returns>
        public static bool RemoveParent(this object o, bool invokeParentChanged = true)
        {
            lock (_lock)
            {
                bool removed = false;
                if (_parents.Contains(o))
                {
                    object parent = _parents[o];

                    _parents.Remove(o);
                    removed = true;

                    if (invokeParentChanged && ParentChanged != null)
                        foreach (ParentChangedEventHandler del in ParentChanged.GetInvocationList())
                            del.BeginInvoke(new ParentOrTagChangedEventArgs(o, parent, null), null, null);
                }
                return removed;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="o"></param>
        /// <returns>True if the object was removed.</returns>
        public static bool RemoveTag(this object o)
        {
            lock (_lock)
            {
                bool removed = false;
                if (_tags.Contains(o))
                {
                    object tag = _tags[o];

                    _tags.Remove(o);
                    removed = true;
                }
                return removed;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="o"></param>
        /// <returns>True if the object was removed.</returns>
        public static bool RemoveDescription(this object o)
        {
            lock (_lock)
            {
                if (_descriptions.Contains(o))
                {
                    _descriptions.Remove(o);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>True if the cache was not empty.</returns>
        public static bool ClearCache(bool invokeParentChanged = true)
        {
            lock (_lock)
            {
                bool cleared = _tags.Count != 0 || _parents.Count != 0 || _descriptions.Count != 0;

                if (invokeParentChanged && ParentChanged != null)
                    foreach (object o in _parents.Keys)
                        foreach (ParentChangedEventHandler del in ParentChanged.GetInvocationList())
                            del.BeginInvoke(new ParentOrTagChangedEventArgs(o, _parents[o], null), null, null);

                _tags.Clear();
                _parents.Clear();
                _descriptions.Clear();
                return cleared;
            }
        }

        /// <summary>
        ///     Returns the string representation of the serialized object --> Must be serializable!
        /// </summary>
        /// <param name="o"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string ToBinaryToString(this object o, string separator = ",")
        {
            byte[] buffer = null;
            using (var ms = new MemoryStream(1))
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, o);
                bf = null;

                buffer = ms.GetBuffer();
            }
            string s = buffer.Combine(separator);
            buffer = null;

            return s;
        }

        public class ParentOrTagChangedEventArgs : EventArgs
        {
            public object Child;
            public object New;
            public object Previous;

            public ParentOrTagChangedEventArgs(object child, object previous, object __new)
            {
                Child = child;
                Previous = previous;
                New = __new;
            }
        }
    }

    public static class DataGridViewExtension
    {
        public static void DoubleBuffered(this DataGridView dgv, bool doubleBuffered)
        {
            Type dgvType = dgv.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                                                  BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dgv, doubleBuffered, null);
        }
    }

    public static class DataGridViewRowExtension
    {
        /// <summary>
        ///     TO CSV for example.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string ToSV(this DataGridViewRow row, string separator)
        {
            if (row.Cells.Count == 0)
                return string.Empty;
            if (row.Cells.Count == 1)
                return row.Cells[0].Value.ToString();

            var sb = new StringBuilder();
            for (int i = 0; i != row.Cells.Count - 1; i++)
            {
                sb.Append(row.Cells[i].Value);
                sb.Append(separator);
            }
            sb.Append(row.Cells[row.Cells.Count - 1].Value);
            return sb.ToString();
        }
    }

    public static class ArrayExtension
    {
        /// <summary>
        ///     Combine a one-dimensional array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string Combine(this Array array, string separator, params object[] exclude)
        {
            if (array.Length == 0) return string.Empty;

            if (exclude == null) exclude = new object[0];

            object value = null;
            var sb = new StringBuilder();
            for (int i = 0; i != array.Length - 1; i++)
            {
                value = array.GetValue(i);
                if (!exclude.Contains(value))
                {
                    sb.Append(value);
                    sb.Append(separator);
                }
            }
            value = array.GetValue(array.Length - 1);
            if (!exclude.Contains(value)) sb.Append(value);

            return sb.ToString();
        }
    }

    public static class ConcurrentBagExtension
    {
        /// <summary>
        ///     A thread safe implementation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="concurrentBag"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static bool Contains<T>(this ConcurrentBag<T> concurrentBag, T item)
        {
            if (concurrentBag.GetTag() == null)
                concurrentBag.SetTag(new object());
            lock (concurrentBag.GetTag())
            {
                foreach (T i in concurrentBag)
                    if (i.Equals(item))
                        return true;
                return false;
            }
        }
    }
}