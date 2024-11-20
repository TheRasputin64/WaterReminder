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
using System.Text;

namespace WaterQuest
{
    public class TrayApplication : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly Dictionary<string, ReminderMessage[]> messagesByCategory;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private volatile bool isRunning = true;
        private const string WORKOUT_FILE = "workouts.json";
        private HashSet<string> notifiedTimes = new HashSet<string>();
        private static readonly Dictionary<string, TimeSpan[]> reminderTimes = new Dictionary<string, TimeSpan[]>
        {
            ["Hydration"] = new[] { new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), new TimeSpan(18, 0, 0), new TimeSpan(20, 0, 0), new TimeSpan(22, 0, 0) },
            ["Workout"] = new[] { new TimeSpan(22, 0, 0), new TimeSpan(23, 0, 0) },
            ["Russian"] = new[] { new TimeSpan(9, 0, 0), new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0) },
            ["CTF"] = new[] { new TimeSpan(19, 0, 0), new TimeSpan(20, 0, 0) },
            ["Sleep"] = new[] { new TimeSpan(23, 30, 0), new TimeSpan(23, 40, 0), new TimeSpan(23, 50, 0), new TimeSpan(0, 0, 0) }
        };
        private static readonly string[] exerciseTypes = { "Push-ups", "Squats", "Jumping Jacks", "Abs", "Advanced Squats", "Bridge" };

        public TrayApplication()
        {
            messagesByCategory = LoadMessages().GroupBy(m => m.category).ToDictionary(g => g.Key, g => g.ToArray());
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(Path.Combine(Application.StartupPath, "heart.ico")),
                ContextMenuStrip = CreateContextMenu(),
                Visible = true
            };
            StartReminderTask();
        }

        private async void StartReminderTask()
        {
            await Task.Run(async () =>
            {
                while (isRunning)
                {
                    try
                    {
                        var now = DateTime.Now;
                        var currentDate = now.Date;

                        if (now.TimeOfDay.Hours == 0 && now.TimeOfDay.Minutes == 0)
                        {
                            notifiedTimes.Clear();
                        }

                        foreach (var category in reminderTimes.Keys)
                        {
                            foreach (var reminderTime in reminderTimes[category])
                            {
                                var notificationKey = $"{category}_{currentDate.ToString("yyyyMMdd")}_{reminderTime:hh\\:mm}";
                                if (!notifiedTimes.Contains(notificationKey))
                                {
                                    var timeDiff = (reminderTime - now.TimeOfDay).TotalMinutes;
                                    if (timeDiff >= -1 && timeDiff <= 0)
                                    {
                                        ShowNotification(category);
                                        notifiedTimes.Add(notificationKey);
                                    }
                                }
                            }
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
            {Width = 300,Height = 450,FormBorderStyle = FormBorderStyle.FixedDialog,Text = "Log Workout",StartPosition = FormStartPosition.CenterScreen};
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
            { Left = 130, Top = yPos, Width = 100, Value = DateTime.Now.Hour < 12 ? DateTime.Now.AddDays(-1) : DateTime.Now };
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
            {MessageBox.Show("No workout history found!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);return;}
            var form = new Form
            {Width = 800,Height = 600,FormBorderStyle = FormBorderStyle.Sizable,Text = "Workout History",StartPosition = FormStartPosition.CenterScreen,MinimumSize = new Size(600, 400)};
            var textBox = new TextBox
            { Multiline = true,ReadOnly = true,ScrollBars = ScrollBars.Vertical,Dock = DockStyle.Fill,Font = new Font("Consolas", 11, FontStyle.Regular),BackColor = Color.White,ForeColor = Color.Black};
            var sb = new StringBuilder();
            var maxExerciseLength = workouts
                .SelectMany(w => w.Exercises.Keys)
                .Max(k => k.Length);
            sb.AppendLine("╔═══════════════════════════════════════════════════════");
            sb.AppendLine("║ WORKOUT HISTORY");
            sb.AppendLine("╠═══════════════════════════════════════════════════════");
            foreach (var workout in workouts.OrderByDescending(w => w.Date))
            {
                sb.AppendLine($"║ DATE: {workout.Date:yyyy-MM-dd} ({workout.Date:dddd})");
                sb.AppendLine("╟───────────────────────────────────────────────────");
                foreach (var exercise in workout.Exercises.OrderBy(e => e.Key))
                {
                    var exerciseName = exercise.Key.PadRight(maxExerciseLength);
                    sb.AppendLine($"║ {exerciseName} │ {exercise.Value,3} reps");
                }
                var totalReps = workout.Exercises.Values.Sum();
                sb.AppendLine("╟───────────────────────────────────────────────────");
                sb.AppendLine($"║ Total Exercises: {workout.Exercises.Count}");
                sb.AppendLine($"║ Total Reps: {totalReps}");
                sb.AppendLine("╠═══════════════════════════════════════════════════════");
            }
            var daysWorkedOut = workouts.Count;
            var totalOverallReps = workouts.Sum(w => w.Exercises.Values.Sum());
            var avgRepsPerWorkout = totalOverallReps / daysWorkedOut;
            sb.AppendLine("║ OVERALL STATISTICS");
            sb.AppendLine("╟───────────────────────────────────────────────────");
            sb.AppendLine($"║ Total Workout Days: {daysWorkedOut}");
            sb.AppendLine($"║ Total Reps: {totalOverallReps}");
            sb.AppendLine($"║ Average Reps per Workout: {avgRepsPerWorkout:F1}");
            sb.AppendLine("╚═══════════════════════════════════════════════════════");
            textBox.Text = sb.ToString();
            form.Controls.Add(textBox);
            form.ShowDialog();
        }
        private void SaveWorkout(WorkoutData workout)
        {
            var workouts = LoadWorkouts();
            var existingWorkout = workouts.FirstOrDefault(w => w.Date == workout.Date);
            if (existingWorkout != null)
            { workouts.Remove(existingWorkout); }
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
            { return new List<WorkoutData>(); }
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
                if (isNew) { Application.Run(new TrayApplication()); }
                else { MessageBox.Show("Already running!"); }
            }
        }
    }
}
