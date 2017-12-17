using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class TasksDisplayEngine
    {
        private static List<string> Tasks = new List<string>();

        public static void SubmitTask(string task)
        {
            Tasks.Add(task);
        }

        /// <summary>
        /// Releases a task. Returns the first item still running in the list or null if no items are in the queue
        /// </summary>
        /// <param name="task">Task to release. All instances with this string name will be released</param>
        /// <returns>Running task or null if none</returns>
        public static string ReleaseTask(string task)
        {
            Tasks.RemoveAll(item => item == task);
            if (Tasks.Count > 0)
            {
                return Tasks[0];
            } else
            {
                return null;
            }
        }

        public static void ClearTasks()
        {
            Tasks.Clear();
        }
    }
}
