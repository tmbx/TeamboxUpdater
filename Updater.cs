/*
 * Updater.cs:
 *   Installer tasks.
 *  
 * Author(s):
 *   François-Denis Gonthier
 * 
 * Copyright (C) 2010-2012 Opersys inc.
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
 * 
 */

using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Schema;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace TeamboxUpdater
{
    /// <summary>
    /// Represents an updatable product on the system.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Locally installed version.
        /// </summary>
        public string LocalVersion;

        /// <summary>
        /// Version that is available on the web. This is null until
        /// the data has been fetched from the web.
        /// </summary>
        public string WebVersion;

        /// <summary>
        /// URL where the installer for the product can be downloaded.
        /// This is null until the data has been fetched from the web.
        /// </summary>
        public string URL;

        /// <summary>
        /// Path where the product installed was saved after download.
        /// This is null until the installer has been fully downloaded.
        /// </summary>
        public string InstallerPath;

        /// <summary>
        /// Human-readable name of the product. This is fetched on the web
        /// so this null at the start of operations.
        /// </summary>
        public string Name;

        /// <summary>
        /// This is set to true if an installed product is installed by another
        /// product and that this product should not be updated by itself.
        /// </summary>
        public bool IncludedElsewhere;
    }

    /// <summary>
    /// Event arguments that gets passed when a task raises an error.
    /// </summary>
    public class TaskErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that caused the event.
        /// </summary>
        public Exception Exception { get; protected set; }

        /// <summary>
        /// An error message.
        /// </summary>
        public string Message { get; protected set; }

        public TaskErrorEventArgs(Exception ex)
        {
            Exception = ex;
        }

        public TaskErrorEventArgs(string s)
        {
            Message = s;
        }
    }

    /// <summary>
    /// Event arguments for the TaskAction event. This means the task is done except
    /// when Partial is set to true. In that case, ProgressPct will also be set to a
    /// value [0-100].
    /// </summary>
    public class TaskActionEventArgs : EventArgs 
    {
        /// <summary>
        /// Set to a value < 100 to update the progress bar in the display
        /// form. Ignored if 0.
        /// </summary>
        public int ProgressPct;

        /// <summary>
        /// This is true if the task isn't done but needs to update its progress.
        /// </summary>
        public bool Partial;
    }

    /// <summary>
    /// Event delegate for the Task.TaskDone event.
    /// </summary>
    public delegate void TaskActionEventHandler(object sender, TaskActionEventArgs ev);

    /// <summary>
    /// Event delegate for the Task.TaskError event.
    /// </summary>
    public delegate void TaskErrorEventHandler(object sender, TaskErrorEventArgs ev);

    public abstract class Task 
    {
        /// <summary>
        /// Called when a task is done or partially done.
        /// </summary>
        public event TaskActionEventHandler TaskDone;

        /// <summary>
        /// Called when a task raises a permanent error.
        /// </summary>
        public event TaskErrorEventHandler TaskError;

        protected void FireTaskDone(TaskActionEventArgs ev)
        {
            if (TaskDone != null) TaskDone(this, ev);
        }

        protected void FireTaskError(TaskErrorEventArgs ev)
        {
            if (TaskError != null) TaskError(this, ev);
        }

        public Product TaskProduct { get; protected set; }
        
        /// <summary>
        /// Cancel a task if possible.
        /// </summary>
        public abstract void Cancel();

        /// <summary>
        /// Start an asynchronous update task.
        /// </summary>
        public abstract void Start();

        public Task(Product product)
        {
            TaskProduct = product;
        }
    }

    public abstract class HttpTask : Task
    {
        /// <summary>
        /// Final response buffer.
        /// </summary>
        private byte[] m_resBuffer;

        /// <summary>
        /// Transfer buffer.
        /// </summary>
        private byte[] m_workBuffer = new byte[2048];

        /// <summary>
        /// Response stream providing data obtained from the URL.
        /// </summary>
        private Stream m_webStream;

        /// <summary>
        /// Request object that will fetch the URL.
        /// </summary>
        private WebRequest m_webRequest;

        /// <summary>
        /// Web response object obtained from the URL.
        /// </summary>
        private WebResponse m_webResponse;

        /// <summary>
        /// Called when the task has finished fetching the content at the demanded URL.
        /// </summary>
        protected virtual void Done()
        {
            m_webStream.Close();
            m_webResponse.Close();
        }
        
        /// <summary>
        /// Total size of the request.
        /// </summary>
        public long ContentSize { get; protected set; }

        /// <summary>
        /// Number of bytes that were downloaded.
        /// </summary>
        public long ContentRead { get; protected set; }

        /// <summary>
        /// Called when a piece of the content has been fetched.
        /// </summary>
        protected abstract void DonePartial(int sz, byte[] partialHttpContent);

        /// <summary>
        /// Called when the asynchronous read requests finishes.
        /// </summary>
        protected virtual void DoneRead(IAsyncResult res)
        {
            try
            {
                int sz = m_webStream.EndRead(res);

                ContentRead += sz;
                DonePartial(sz, m_workBuffer);

                // Test if we are done.
                if (ContentRead == ContentSize)
                {
                    // Call the function that says we are done with the download.
                    Done();
                }
                else
                {
                    // If we are not done, keeping reading in the background.
                    m_webStream.BeginRead(m_workBuffer, 0, m_workBuffer.Length, new AsyncCallback(DoneRead), null);
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.RequestCanceled) return;
                else
                    FireTaskError(new TaskErrorEventArgs(ex));
            }
            catch (Exception ex)
            {
                FireTaskError(new TaskErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// Happens when the response content is ready to be fetched.
        /// </summary>
        protected virtual void DoneGetResponse(IAsyncResult res)
        {
            try
            {
                m_webRequest.EndGetResponse(res);

                m_webResponse = m_webRequest.GetResponse();
                m_webStream = m_webResponse.GetResponseStream();

                // Get enough memory for the result.
                m_resBuffer = new byte[m_webResponse.ContentLength];

                ContentSize = m_webResponse.ContentLength;

                // Start asynchronous read from the buffer.
                m_webStream.BeginRead(m_workBuffer, 0, m_workBuffer.Length, new AsyncCallback(DoneRead), null);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.RequestCanceled) return;
                else
                    FireTaskError(new TaskErrorEventArgs(ex));
            }
            catch (Exception ex)
            {
                FireTaskError(new TaskErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// Start by getting the response header asynchronously.
        /// </summary>
        public override void Start()
        {
            m_webRequest.BeginGetResponse(new AsyncCallback(DoneGetResponse), null);
        }

        /// <summary>
        /// Cancel the request. This will make the asynchronous calls throw but 
        /// this is handled.
        /// </summary>
        public override void Cancel()
        {
            m_webRequest.Abort();
        }

        public HttpTask(string URL, Product product)
            : base(product)
        {
            m_webRequest = HttpWebRequest.Create(URL);
        }
    }

    /// <summary>
    /// Subtask that fetches an URL containing an XML file, containing the information
    /// on the product to check for update.
    /// </summary>
    public class WebCheck : HttpTask
    {
        private byte[] m_resContent;

        /// <summary>
        /// Accumulate the buffer content inside a buffer.
        /// </summary>
        protected override void DonePartial(int sz, byte[] partialHttpContent)
        {
            TaskActionEventArgs ev = new TaskActionEventArgs();

            ev.Partial = true;
            ev.ProgressPct = (int)(((float)ContentRead / (float)ContentSize) * 100);

            // Create a static transfer buffer.
            if (m_resContent == null) m_resContent = new byte[ContentSize];

            // Copy the content inside the buffer.
            Array.Copy(partialHttpContent, 0, m_resContent, ContentRead - sz, sz);

            // Raise a partial completion.
            FireTaskDone(ev);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void Done()
        {
            bool validSchema = true;
            XmlDocument xmlDoc = new XmlDocument();
            XmlSchemaSet xmlSchemas = new XmlSchemaSet();
            XmlNamespaceManager xmlNS;
            string docNS = "http://www.teambox.co/Updates";

            base.Done();

            xmlSchemas.Add(docNS, XmlReader.Create(new StringReader(Properties.Resources.UpdateSchema)));
            xmlDoc.Schemas = xmlSchemas;

            xmlDoc.LoadXml(Encoding.ASCII.GetString(m_resContent));
            xmlNS = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlNS.AddNamespace("k", docNS);

            xmlDoc.Validate(new ValidationEventHandler(delegate (object sender, ValidationEventArgs ev) {
                // Trap any kind of validation error.
                validSchema = false;
                FireTaskError(new TaskErrorEventArgs(ev.Exception));                
            }));

            if (!validSchema) return;

            XmlNodeList productNodeList = xmlDoc.SelectNodes("/k:teambox/k:product", xmlNS);

            foreach (XmlNode productNode in productNodeList)
            {
                string xID = productNode.SelectSingleNode("k:id", xmlNS).InnerText;

                TaskProduct.WebVersion = productNode.SelectSingleNode("k:version", xmlNS).InnerText;
                TaskProduct.URL = productNode.SelectSingleNode("k:url", xmlNS).InnerText;
            }

            // Tell the caller we are done.
            FireTaskDone(new TaskActionEventArgs());
        }

        public WebCheck(Product product)
            : base(product.URL, product)
        {
        }
    }

    /// <summary>
    /// Subtask that downloads the updater files.
    /// </summary>
    public class WebDownload : HttpTask
    {
        /// <summary>
        /// The file stream where the installer content is written.
        /// </summary>
        private FileStream m_fileStream;

        /// <summary>
        /// Temporary path to the installer.
        /// </summary>
        private string m_tmpInstallerPath;

        /// <summary>
        /// Called when a part of the file that has been downloaded
        /// has been written on the disk.
        /// </summary>
        protected void DoneWrite(IAsyncResult res)
        {
            try
            {
                m_fileStream.EndWrite(res);
            }
            catch (Exception ex)
            {
                FireTaskError(new TaskErrorEventArgs(ex));
            }

            // If the download is done flush and close the file.
            if (m_fileStream.Length == ContentSize)
            {
                m_fileStream.Flush();
                m_fileStream.Close();
                m_fileStream = null;

                // The installer is accessible now.
                TaskProduct.InstallerPath = m_tmpInstallerPath;

                // Raise the event saying the task is finished.
                FireTaskDone(new TaskActionEventArgs());
            }
        }

        /// <summary>
        /// HTTP content being downloaded
        /// </summary>
        protected override void DonePartial(int sz, byte[] partialRes)
        {
            TaskActionEventArgs ev = new TaskActionEventArgs();

            try
            {
                ev.Partial = true;
                ev.ProgressPct = (int)(((float)ContentRead / (float)ContentSize) * 100);

                // Write the downloaded content asynchronously.
                byte[] writeRes = new byte[partialRes.Length];
                partialRes.CopyTo(writeRes, 0);
                m_fileStream.BeginWrite(writeRes, 0, sz, new AsyncCallback(DoneWrite), null);

                // Raise a partial result.
                FireTaskDone(ev);
            }
            catch (Exception ex)
            {
                FireTaskError(new TaskErrorEventArgs(ex));
            }
        }

        public WebDownload(Product product)
            : base(product.URL, product)
        {
            Uri uri = new Uri(product.URL);
            string fileName = Path.GetFileName(uri.PathAndQuery);

            // Prepare to save the installer inside the user's temporary directory.
            m_tmpInstallerPath = Path.Combine(Path.GetTempPath(), fileName);

            // Create a file stream to save the file while downloading.
            m_fileStream = new FileStream(m_tmpInstallerPath, FileMode.Create, FileAccess.Write);
        }
    }

    public class Installer : Task
    {
        /// <summary>
        /// Nothing to cancel at this point since we don't want to kill
        /// a running installer.
        /// </summary>
        public override void Cancel()
        {
        }

        /// <summary>
        /// Start the install process and fork a thread that will wait for
        /// it to finish.
        /// </summary>
        public override void Start()
        {
            Process insProcess = new Process();
            ProcessStartInfo insProcessInfo = new ProcessStartInfo();
            Thread th;

            insProcessInfo.FileName = TaskProduct.InstallerPath;

            th = new Thread(new ThreadStart(delegate()
                {
                    try
                    {
                        insProcess = Process.Start(insProcessInfo);
                        insProcess.WaitForExit();

                        // Return an error if the installer showhow fails.
                        if (insProcess.ExitCode != 0)
                            FireTaskError(new TaskErrorEventArgs("Installer returned an error."));
                        else
                            FireTaskDone(new TaskActionEventArgs());
                    }
                    catch (Exception ex)
                    {
                        FireTaskError(new TaskErrorEventArgs(ex));
                    }
                }));
            th.Start();
        }

        public Installer(Product product)
            : base(product)
        {
        }
    }
}
