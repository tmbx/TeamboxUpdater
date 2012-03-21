/*
 * ProgressForm.cs:
 *   Progress display form.
 *  
 * Author(s):
 *   François-Denis Gonthier
 * 
 * Copyright (C) 2010-2012 Opersys inc.
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; version 2
 * of the License, not any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 */

using System;
using System.Windows.Forms;

namespace TeamboxUpdater
{
    /// <summary>
    /// Progress display form.
    /// </summary>
    public partial class ProgressForm : Form
    {
        public event EventHandler Cancelled;

        // This form has been coded with in mind the fact that it may 
        // not have been loaded. Methods and properties in this form
        // must also make sure to use BeginInvoke before touching controls
        // since we never know on which thread we are executing.

        public ProgressForm()
        {
            InitializeComponent();
        }

        private int m_progress;
        
        /// <summary>
        /// Change the progress %.
        /// </summary>
        public int Progress
        {
            get
            {
                return m_progress;
            }
            set
            {
                m_progress = value;
                if (m_loaded)
                {
                    this.BeginInvoke(new EmptyDelegate(delegate()
                        {
                            prgPct.Value = m_progress;
                        }));
                }
            }
        }

        /// <summary>
        /// This overrides the Form.Close for no other reasons than consistency
        /// with the ProgressTray class.
        /// </summary>
        public new void Close()
        {
            if (m_loaded)
            {
                this.BeginInvoke(new EmptyDelegate(delegate()
                    {
                        this.Dispose();
                    }));
            }
        }

        /// <summary>
        /// Prevent winforms from disposing of the form.
        /// </summary>
        /// <param name="e"></param>
        protected override void  OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            base.OnClosing(e);

            ToggleVisible();
        }

        /// <summary>
        /// Value of the status label on the forum.
        /// </summary>
        private string m_statusText;
        
        /// <summary>
        /// Status text to display below the progress bar.
        /// </summary>
        public string Status
        {
            get 
            {
                return m_statusText;
            }
            set
            {
                m_statusText = value;
                if (m_loaded)
                {
                    this.BeginInvoke(new EmptyDelegate(delegate()
                        {
                            lblStatus.Text = value;
                        }));
                }
            }
        }

        /// <summary>
        /// Value of the Enabled property.
        /// </summary>
        private bool m_cancellable = true;

        /// <summary>
        /// Determines if the currently running task is cancellable.
        /// </summary>
        public bool Cancellable
        {
            get
            {
                return m_cancellable;
            }
            set
            {
                m_cancellable = value;
                if (m_loaded)
                {
                    this.BeginInvoke(new EmptyDelegate(delegate()
                        {
                            btnCancel.Enabled = value;
                        }));
                }
            }
        }

        /// <summary>
        /// Toggle the visibility of the form, loading it if it has not been loaded.
        /// </summary>
        public void ToggleVisible()
        {
            if (!m_loaded) this.Show();
            else this.Visible = !this.Visible;
        }

        /// <summary>
        /// This is set to 'True' once the form has been loaded.
        /// </summary>
        private bool m_loaded;

        /// <summary>
        /// Mark the form as loaded and initialize the controls with the
        /// correct value.
        /// </summary>
        private void ProgressForm_Load(object sender, EventArgs e)
        {
            m_loaded = true;
            lblStatus.Text = m_statusText;
            prgPct.Value = m_progress;
            btnCancel.Enabled = m_cancellable;
        }

        /// <summary>
        /// Mark the form as not loaded.
        /// </summary>
        private void ProgressForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            m_loaded = false;
        }

        /// <summary>
        /// Forward the event to the main program.
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (Cancelled != null) Cancelled(sender, e);
        }
    }
}
