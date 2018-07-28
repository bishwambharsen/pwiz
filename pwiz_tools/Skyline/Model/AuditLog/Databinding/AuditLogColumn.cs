﻿/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Drawing;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogColumn : TextImageColumn
    {
        private readonly Image[] _images;

        public AuditLogColumn()
        {
            var undoRedoImage = new Bitmap(Resources.Edit_Undo);
            undoRedoImage.MakeTransparent(Color.Magenta);
            var undoRedoMultipleImage = new Bitmap(Resources.Edit_Undo_Multiple);
            undoRedoMultipleImage.MakeTransparent(Color.Magenta);
            _images = new Image[]
            {
                undoRedoImage,
                undoRedoMultipleImage,
                Resources.magnifier_zoom_in
            };
        }

        public override bool ShouldDisplay(object cellValue, int imageIndex)
        {
            var value = cellValue as AuditLogRow.AuditLogRowText;
            if (value == null)
                return false;

            switch (imageIndex)
            {
                case 0:
                    return value.UndoAction != null && !value.IsMultipleUndo;
                case 1:
                    return value.UndoAction != null && value.IsMultipleUndo;
                case 2:
                    return !string.IsNullOrEmpty(value.ExtraInfo);
                default:
                    return false;
            }
        }

        public override void OnClick(object cellValue, int imageIndex)
        {
            var value = cellValue as AuditLogRow.AuditLogRowText;
            if (value == null)
                return;

            switch (imageIndex)
            {
                case 0:
                case 1:
                {
                    value.UndoAction();
                    break;
                }
                case 2:
                {
                    using (var form = new AuditLogExtraInfoForm(value.Text, value.ExtraInfo))
                    {
                        form.ShowDialog(DataGridView.FindForm());
                    }
                    break;
                }
            }    
        }

        public override IList<Image> Images
        {
            get { return _images; }
        }
    }
}