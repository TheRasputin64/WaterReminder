using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using System.Drawing;

namespace WaterQuest
{
    public sealed class MainForm : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly Dictionary<string, ReminderMessage[]> messagesByCategory;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private volatile bool isRunning = true;
        private const string WORKOUT_FILE = "workouts.json";

        private static readonly Dictionary<string, TimeSpan[]> reminderTimes = new Dictionary<string, TimeSpan[]>
        {
            ["Hydration"] = new[] { new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0),
                                  new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), new TimeSpan(18, 0, 0) },
            ["Workout"] = new[] { new TimeSpan(22, 0, 0), new TimeSpan(23, 0, 0) },
            ["Russian"] = new[] { new TimeSpan(9, 0, 0), new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0) },
            ["CTF"] = new[] { new TimeSpan(19, 0, 0), new TimeSpan(20, 0, 0) },
            ["Sleep"] = new[] { new TimeSpan(23, 30, 0), new TimeSpan(23, 40, 0), new TimeSpan(23, 50, 0), new TimeSpan(0, 0, 0) }
        };

        private static readonly string[] exerciseTypes = { "Push-ups", "Squats", "Jumping Jacks", "Abs", "Advanced Squats" };

        public MainForm()
        {
            InitializeForm();
            messagesByCategory = LoadMessages().GroupBy(m => m.category).ToDictionary(g => g.Key, g => g.ToArray());
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(Path.Combine(Application.StartupPath, "heart.ico")),
                ContextMenuStrip = CreateContextMenu(),
                Visible = true
            };
            StartReminderTask();
        }

        private void InitializeForm()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            var workoutMenu = new ToolStripMenuItem("Workout");
            workoutMenu.DropDownItems.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem("Log Today's Workout", null, (s,e) => ShowWorkoutForm()),
                new ToolStripMenuItem("View History", null, (s,e) => ShowWorkoutHistory())
            });

            var testsMenu = new ToolStripMenuItem("Tests");
            testsMenu.DropDownItems.AddRange(
                reminderTimes.Keys.Select(category =>
                    new ToolStripMenuItem(category, null, (s, e) => ShowNotification(category)))
                .ToArray());

            menu.Items.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem("Check Next Reminder", null, (s,e) => ShowNextReminder()),
                workoutMenu,
                testsMenu,
                new ToolStripMenuItem("Exit", null, (s,e) => { isRunning = false; cts.Cancel(); Application.Exit(); })
            });
            return menu;
        }

        private void ShowWorkoutForm()
        {
            var form = new Form
            {
                Width = 300,
                Height = 400,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Log Workout",
                StartPosition = FormStartPosition.CenterScreen
            };

            var exercises = new Dictionary<string, NumericUpDown>();
            int yPos = 20;

            foreach (var exercise in exerciseTypes)
            {
                form.Controls.Add(new Label { Left = 20, Top = yPos, Text = exercise, Width = 100 });
                var numericUpDown = new NumericUpDown { Left = 130, Top = yPos, Width = 100, Maximum = 1000 };
                form.Controls.Add(numericUpDown);
                exercises.Add(exercise, numericUpDown);
                yPos += 40;
            }

            form.Controls.Add(new Label { Left = 20, Top = yPos, Text = "Date:", Width = 100 });
            var datePicker = new DateTimePicker
            {
                Left = 130,
                Top = yPos,
                Width = 100,
                Value = DateTime.Now.Hour < 12 ? DateTime.Now.AddDays(-1) : DateTime.Now
            };
            form.Controls.Add(datePicker);

            var saveButton = new Button { Text = "Save", Left = 100, Top = yPos + 40, Width = 100 };
            saveButton.Click += (s, e) =>
            {
                SaveWorkout(new WorkoutData
                {
                    Date = datePicker.Value.Date,
                    Exercises = exercises.ToDictionary(kvp => kvp.Key, kvp => (int)kvp.Value.Value)
                });
                form.Close();
            };
            form.Controls.Add(saveButton);
            form.ShowDialog();
        }

        private void ShowWorkoutHistory()
        {
            var workouts = LoadWorkouts();
            if (!workouts.Any())
            {
                MessageBox.Show("No workout history found!");
                return;
            }

            var form = new Form
            {
                Width = 500,
                Height = 400,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Workout History",
                StartPosition = FormStartPosition.CenterScreen
            };

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill
            };

            var history = workouts
                .OrderByDescending(w => w.Date)
                .Select(w => $"Date: {w.Date:yyyy-MM-dd}\n" +
                    string.Join("\n", w.Exercises.Select(e => $"{e.Key}: {e.Value}")) +
                    "\n\n");

            textBox.Text = string.Join("", history);
            form.Controls.Add(textBox);
            form.ShowDialog();
        }

        private void SaveWorkout(WorkoutData workout)
        {
            var workouts = LoadWorkouts();
            var existingWorkout = workouts.FirstOrDefault(w => w.Date == workout.Date);
            if (existingWorkout != null)
            {
                workouts.Remove(existingWorkout);
            }
            workouts.Add(workout);

            File.WriteAllText(
                Path.Combine(Application.StartupPath, WORKOUT_FILE),
                JsonSerializer.Serialize(workouts, new JsonSerializerOptions { WriteIndented = true })
            );
            MessageBox.Show("Workout saved successfully!");
        }

        private List<WorkoutData> LoadWorkouts()
        {
            try
            {
                var path = Path.Combine(Application.StartupPath, WORKOUT_FILE);
                if (!File.Exists(path)) return new List<WorkoutData>();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<WorkoutData>>(json) ?? new List<WorkoutData>();
            }
            catch
            {
                return new List<WorkoutData>();
            }
        }

        private List<ReminderMessage> LoadMessages()
        {
            try
            {
                using (var fs = File.OpenRead(Path.Combine(Application.StartupPath, "messages.json")))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<ReminderMessage>>>(fs);
                    return data != null && data.ContainsKey("messages") ? data["messages"] : new List<ReminderMessage>();
                }
            }
            catch { return new List<ReminderMessage>(); }
        }

        private async void StartReminderTask()
        {
            await Task.Run(async () =>
            {
                while (isRunning)
                {
                    try
                    {
                        var now = DateTime.Now.TimeOfDay;
                        foreach (var category in reminderTimes.Keys)
                        {
                            if (reminderTimes[category].Any(t => Math.Abs((now - t).TotalMinutes) < 1))
                                ShowNotification(category);
                        }
                        await Task.Delay(30000, cts.Token);
                    }
                    catch (OperationCanceledException) { break; }
                }
            });
        }

        private void ShowNotification(string category)
        {
            if (!messagesByCategory.ContainsKey(category)) return;
            var message = messagesByCategory[category][new Random().Next(messagesByCategory[category].Length)];
            new ToastContentBuilder()
                .AddText(message.message)
                .AddAttributionText($"{message.type} | {DateTime.Now.ToShortTimeString()}")
                .Show();
        }

        private void ShowNextReminder()
        {
            var now = DateTime.Now.TimeOfDay;
            var nextReminders = reminderTimes.ToDictionary(
                kvp => kvp.Key,
                kvp => GetNextReminderTime(kvp.Value, now)
            );

            MessageBox.Show(string.Join("\n",
                nextReminders.Select(kvp => $"Next {kvp.Key}: {kvp.Value:hh\\:mm}")));
        }

        private TimeSpan GetNextReminderTime(TimeSpan[] times, TimeSpan now)
        {
            var next = times.OrderBy(t => t).FirstOrDefault(t => t > now);
            return next == default(TimeSpan) ? times[0] : next;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isRunning = false;
                cts.Cancel();
                cts.Dispose();
                if (trayIcon != null)
                    trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class WorkoutData
    {
        public DateTime Date { get; set; }
        public Dictionary<string, int> Exercises { get; set; }
    }

    public class ReminderMessage
    {
        public string message { get; set; }
        public string type { get; set; }
        public string category { get; set; }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var mutex = new Mutex(true, "WaterQuestMutex", out bool isNew))
            {
                if (isNew) Application.Run(new MainForm());
                else MessageBox.Show("Already running!");
            }
        }
    }
}
