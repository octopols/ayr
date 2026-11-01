using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// Tasks whose author is MSI or whose program is MSI's. Each task is an XML file under System32\Tasks, mirroring its
    /// path in Task Scheduler.
    /// </summary>
    public sealed class ScheduledTaskSource : IMsiComponentSource
    {
        private const string TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        private static string TasksFolder => Path.Combine(Environment.SystemDirectory, "Tasks");

        public IEnumerable<MsiComponent> Find()
        {
            foreach (string file in TaskFiles(TasksFolder))
            {
                ScheduledTask task = Read(file);
                if (task != null)
                {
                    yield return task;
                }
            }
        }

        // Folder by folder, so one folder Windows keeps closed doesn't hide the tasks in all the others.
        private static IEnumerable<string> TaskFiles(string folder)
        {
            string[] files, subfolders;
            try
            {
                files = Directory.GetFiles(folder);
                subfolders = Directory.GetDirectories(folder);
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }
            foreach (string file in files)
            {
                yield return file;
            }
            foreach (string subfolder in subfolders)
            {
                foreach (string file in TaskFiles(subfolder))
                {
                    yield return file;
                }
            }
        }

        // A task file that can't be read or parsed isn't MSI's to judge; it is skipped rather than failing the scan.
        private static ScheduledTask Read(string file)
        {
            var document = new XmlDocument();
            try
            {
                document.Load(file);
            }
            catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
            var names = new XmlNamespaceManager(document.NameTable);
            names.AddNamespace("t", TaskNamespace);
            string author = document.SelectSingleNode("/t:Task/t:RegistrationInfo/t:Author", names)?.InnerText;
            string program = CommandPath.ProgramOf(document.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command", names)?.InnerText);
            if (!MicroStar.IsPublisher(author) && !MicroStar.OwnsFile(program))
            {
                return null;
            }
            return new ScheduledTask(file.Substring(TasksFolder.Length), program ?? author);
        }
    }
}
