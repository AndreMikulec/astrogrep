﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using AstroGrep.Common;
using AstroGrep.Common.Logging;
using AstroGrep.Windows;

namespace AstroGrep.Core
{
   /// <summary>
   /// Helper class to handle managing the text editors.
   /// </summary>
   /// <remarks>
   ///   AstroGrep File Searching Utility. Written by Theodore L. Ward
   ///   Copyright (C) 2002 AstroComma Incorporated.
   ///   
   ///   This program is free software; you can redistribute it and/or
   ///   modify it under the terms of the GNU General Public License
   ///   as published by the Free Software Foundation; either version 2
   ///   of the License, or (at your option) any later version.
   ///   
   ///   This program is distributed in the hope that it will be useful,
   ///   but WITHOUT ANY WARRANTY; without even the implied warranty of
   ///   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   ///   GNU General Public License for more details.
   ///   
   ///   You should have received a copy of the GNU General Public License
   ///   along with this program; if not, write to the Free Software
   ///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
   /// 
   ///   The author may be contacted at:
   ///   ted@astrocomma.com or curtismbeard@gmail.com
   /// </remarks>
   /// <history>
   /// [Curtis_Beard]      12/06/2012  ADD: create common class to host text editor methods
   /// </history>
   public class TextEditors
   {
      private static TextEditor[] __TextEditors;
      private const string DELIMETER = "|;;|";

      /// <summary>
      /// Edit file from given MatchResult at first match if available.
      /// </summary>
      /// <param name="match">Current MatchResult</param>
      /// <history>
      /// [Curtis_Beard]	   05/27/2015	FIX: 73, open text editor even when no first match (usually during file only search)
      /// </history>
      public static void EditFile(libAstroGrep.MatchResult match)
      {
         if (match != null)
         {
            // open the default editor at first match
            var lineNumber = 1;
            var columnNumber = 1;
            var lineText = string.Empty;

            var matchLine = match.GetFirstMatch();
            if (matchLine != null)
            {
               lineNumber = matchLine.LineNumber;
               columnNumber = matchLine.ColumnNumber;
               lineText = matchLine.Line;
            }
            
            EditFile(match.File.FullName, lineNumber, columnNumber, lineText);
         }
      }

      /// <summary>
      /// Edit a file that the user has double clicked on.
      /// </summary>
      /// <param name="opener">TextEditorOpener object containing the information necessary top edit a file.</param>
      /// <history>
      /// [Curtis_Beard]	   06/25/2015	Initial
      /// </history>
      public static void EditFile(TextEditorOpener opener)
      {
         if (opener != null && opener.HasValue())
         {
            EditFile(opener.Path, opener.LineNumber, opener.ColumnNumber, opener.LineText);
         }
      }

      /// <summary>
      /// Edit a file that the user has double clicked on
      /// </summary>
      /// <param name="path">Fully qualified file path</param>
      /// <param name="line">Line number</param>
      /// <param name="column">Column position</param>
      /// <param name="lineText">Current line</param>
      /// <history>
      /// [Theodore_Ward]     ??/??/????  Initial
      /// [Curtis_Beard]	   01/11/2005	.Net Conversion, Try/Catch
      /// [Curtis_Beard]	   06/13/2005	CHG: Used new cmd line arg specification
      /// [Curtis_Beard]	   07/20/2006	CHG: Run the text editor associated with the file's extension
      /// [Curtis_Beard]	   07/26/2006	ADD: 1512026, column position
      /// [Curtis_Beard]      09/28/2012  CHG: 3553474, support multiple file types per editor
      /// [Curtis_Beard]		04/07/2015	CHG: check for a valid line text before using
      /// [Curtis_Beard]	   04/08/2015	CHG: add logging
      /// [Curtis_Beard]	   08/20/2015	FIX: 81, use associated app instead of displaying message
      /// </history>
      public static void EditFile(string path, int line, int column, string lineText)
      {
         try
         {
            // pick the correct editor to use
            System.IO.FileInfo file = new System.IO.FileInfo(path);
            TextEditor editorToUse = null;

            // find extension match
            if (__TextEditors != null)
            {
               foreach (TextEditor editor in __TextEditors)
               {
                  // handle multiple types for one editor
                  string[] types = new string[1] { editor.FileType };
                  if (editor.FileType.Contains(Constants.TEXT_EDITOR_TYPE_SEPARATOR))
                  {
                     types = editor.FileType.Split(Constants.TEXT_EDITOR_TYPE_SEPARATOR.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                  }

                  // loop through all types defined for this editor
                  foreach (string type in types)
                  {
                     string currentType = type;

                     // add missing start . if file type has it and the user didn't add it.
                     if (currentType != Constants.ALL_FILE_TYPES && !currentType.StartsWith(".") && file.Extension.StartsWith("."))
                        currentType = string.Format(".{0}", currentType);

                     if (currentType.IndexOf(file.Extension, StringComparison.OrdinalIgnoreCase) > -1)
                     {
                        // use this editor
                        editorToUse = editor;
                        break;
                     }
                  }

                  if (editorToUse != null)
                     break;
               }

               // try finding default for all types (*)
               if (editorToUse == null)
               {
                  foreach (TextEditor editor in __TextEditors)
                  {
                     if (editor.FileType.Equals(Constants.ALL_FILE_TYPES))
                     {
                        // use this editor
                        editorToUse = editor;
                        break;
                     }
                  }
               }

               if (editorToUse == null)
               {
                  // since nothing defined, just use default app associated with file type
                  OpenFileWithDefaultApp(path);
               }
               else
               {
                  // adjust column if tab size is set
                  if (editorToUse.TabSize > 0 && column > 0 && !string.IsNullOrEmpty(lineText))
                  {
                     // count how many tabs before found hit column index
                     int count = 0;
                     for (int i = column - 1; i >= 0; i--)
                     {
                        if (lineText[i] == '\t')
                        {
                           count++;
                        }
                     }

                     column += ((count * editorToUse.TabSize) - count);
                  }

                  OpenEditor(editorToUse, path, line, column);
               }
            }
         }
         catch (Exception ex)
         {
            LogClient.Instance.Logger.Error("Unable to open text editor for file {0} at line {1}, column {2}, with text {3} and message {4}", path, line, column, lineText, ex.Message);

            MessageBox.Show(String.Format(Language.GetGenericText("TextEditorsErrorGeneric"), path, ex.Message),
                  ProductInformation.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
         }
      }

      /// <summary>
      /// Loads the user specified text editors.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]	   07/10/2006   Created
      /// [Curtis_Beard]	   11/07/2012   Renamed
      /// </history>
      public static void Load()
      {
         string editorsString = AstroGrep.Core.GeneralSettings.TextEditors;

         if (editorsString.Length > 0)
         {
            //parse string for each editor
            string[] editors = Utils.SplitByString(editorsString, DELIMETER);
            if (editors.Length > 0)
            {
               __TextEditors = new TextEditor[editors.Length];

               for (int i = 0; i < editors.Length; i++)
               {
                  //parse each editor for class properties
                  __TextEditors[i] = TextEditor.FromString(editors[i]);
               }
            }
         }
         else
         {
            __TextEditors = Windows.Legacy.ConvertTextEditors();
            Save(__TextEditors);
         }
      }

      /// <summary>
      /// Get the text editors that were loaded.
      /// </summary>
      /// <returns>Array of TextEditor objects</returns>
      /// <history>
      /// [Curtis_Beard]	   07/10/2006	Created
      /// [Curtis_Beard]	   11/07/2012	Renamed
      /// </history>
      public static TextEditor[] GetAll()
      {
         return __TextEditors;
      }

      /// <summary>
      /// Saves the given Array of TextEditor objects.
      /// </summary>
      /// <param name="editors">Array of TextEditor objects</param>
      /// <history>
      /// [Curtis_Beard]	   07/10/2006	Created
      /// [Curtis_Beard]	   11/07/2012	Renamed
      /// </history>
      public static void Save(TextEditor[] editors)
      {
         if (editors != null)
         {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(editors.Length);
            __TextEditors = new TextEditor[editors.Length];
            __TextEditors = editors;

            foreach (TextEditor editor in editors)
            {
               if (builder.Length > 0)
               {
                  builder.Append(DELIMETER);
               }

               builder.Append(editor.ToString());
            }

            AstroGrep.Core.GeneralSettings.TextEditors = builder.ToString();
         }
         else
         {
            __TextEditors = null;
            AstroGrep.Core.GeneralSettings.TextEditors = string.Empty;
         }

         AstroGrep.Core.GeneralSettings.Save();
      }

      /// <summary>
      /// Opens the given file using the default associated application.
      /// </summary>
      /// <param name="path">Full path to file</param>
      /// <history>
      /// [Curtis_Beard]      02/12/2014  ADD: 67, open selected file(s) with associated application
      /// </history>
      public static void OpenFileWithDefaultApp(string path)
      {
         System.Diagnostics.Process.Start(path);
      }

      #region Private Methods

      /// <summary>
      /// Open the defined editor for a file that the user has double clicked on.
      /// </summary>
      /// <param name="textEditor">Text editor object reference</param>
      /// <param name="path">Fully qualified file path</param>
      /// <param name="line">Line number</param>
      /// <param name="column">Column position</param>
      /// <history>
      /// [Curtis_Beard]	   07/10/2006	ADD: Initial
      /// [Curtis_Beard]	   07/26/2006	ADD: 1512026, column position
      /// [Curtis_Beard]	   08/13/2014	ADD: 80, add ability to open default app when no editor is specified
      /// [Curtis_Beard]		03/06/2015	FIX: 65, check editor for using quotes around file name, cleanup
      /// [Curtis_Beard]	   04/08/2015	CHG: add logging
      /// [Curtis_Beard]	   08/20/2015	CHG: 80, make check for empty editor to use default app the first check.
      /// </history>
      private static void OpenEditor(TextEditor textEditor, string path, int line, int column)
      {
         try
         {
            if (string.IsNullOrEmpty(textEditor.Editor))
            {
               OpenFileWithDefaultApp(path);
            }
            else if (textEditor.Arguments.IndexOf("%1") == -1)
            {
               // no file argument specified
               MessageBox.Show(Language.GetGenericText("TextEditorsErrorNoCmdLineForFile"),
                  ProductInformation.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
               // replace
               //  %1 with filename 
               //  %2 with line number
               //  %3 with column
               string args = textEditor.Arguments;
               if (textEditor.UseQuotesAroundFileName)
               {
                  path = "\"" + path + "\"";
               }
               args = args.Replace("%1", path);
               args = args.Replace("%2", line.ToString());
               args = args.Replace("%3", column.ToString());

               System.Diagnostics.Process.Start(textEditor.Editor, args);
            }
         }
         catch (Exception ex)
         {
            LogClient.Instance.Logger.Error("Unable to open text editor for editor {0}, file {1} at line {2}, column {3}, with message {4}", textEditor.ToString(), path, line, column, ex.Message);

            MessageBox.Show(String.Format(Language.GetGenericText("TextEditorsErrorGeneric"), path, ex.Message),
               ProductInformation.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
         }
      }

      #endregion

      /// <summary>
      /// TextEditor opener object to wrap around necessary values to edit a file.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]	   06/25/2015	Initial
      /// </history>
      public class TextEditorOpener
      {
         /// <summary>Full file path</summary>
         public string Path { get; set; }
         /// <summary>Line number</summary>
         public int LineNumber { get; set; }
         /// <summary>Column number</summary>
         public int ColumnNumber { get; set; }
         /// <summary>Current line's text</summary>
         public string LineText { get; set; }

         /// <summary>
         /// Create an instance of this class.
         /// </summary>
         public TextEditorOpener()
         {
            Path = string.Empty;
            LineNumber = 1;
            ColumnNumber = 1;
            LineText = string.Empty;
         }

         /// <summary>
         /// Create an instance of this class.
         /// </summary>
         /// <param name="path">full file path</param>
         /// <param name="lineNumber">line number</param>
         /// <param name="columnNumber">column number</param>
         /// <param name="lineText">current line's text</param>
         public TextEditorOpener(string path, int lineNumber, int columnNumber, string lineText)
            : this()
         {
            Path = path;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            LineText = lineText;
         }

         /// <summary>
         /// Determines if object has a value or is empty.
         /// </summary>
         /// <returns>true if value present, false otherwise</returns>
         public bool HasValue()
         {
            return !string.IsNullOrEmpty(Path);
         }
      }
   }
}
