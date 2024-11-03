using System;
using System.Threading;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

class Program
{
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    private const int SW_NORMAL = 1;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOSIZE = 0x0001;
    private const int CONSOLE_WIDTH = 100;
    private const int CONSOLE_HEIGHT = 21;

    static string heart = "♥";
    static int selected = 0;
    static Random rnd = new Random();
    static int playerHP = 20;
    static int maxHP = 20;

    static readonly int choiceY = 17;
    static readonly int dialogBoxWidth = 90;
    static readonly int dialogBoxStartX = 6;
    static readonly int dialogBoxCenterX = dialogBoxStartX + (dialogBoxWidth / 2);
    static readonly int spaceBetweenChoices = 20;

    static readonly int yesX = dialogBoxCenterX - (spaceBetweenChoices / 2) - 1;
    static readonly int noX = dialogBoxCenterX + (spaceBetweenChoices / 2) - 1;
    static readonly int heartOffsetX = -2;

    static List<string> dialogues = new List<string>
    {
        "* A little water spirit appears.\n* HEY! DRINK SOME WATER, OKAY?",
        "* A bright flower pops up smiling.\n* THIRSTY? WATER MAKES YOU FEEL GOOD!",
        "* A glowing water drop materializes.\n* DRINK UP! YOU’LL FEEL STRONG!",
        "* A friendly stream flows nearby, calling out.\n* LET’S DRINK WATER! ",
        "* A playful raindrop dances in front of you.\n* TIME TO HYDRATE! JUST ONE SIP!"

    };

    static void Main(string[] args)
    {
        SetupConsole();
        while (true)
        {
            RunDialog();
            Thread.Sleep(1000);
            Console.Clear();
        }
    }

    static void SetupConsole()
    {
        Console.Title = "UNDERTALE";
        Console.OutputEncoding = Encoding.Unicode;
        Console.CursorVisible = false;
        Console.SetWindowSize(CONSOLE_WIDTH, CONSOLE_HEIGHT);
        Console.SetBufferSize(CONSOLE_WIDTH, CONSOLE_HEIGHT);

        IntPtr handle = GetConsoleWindow();
        int x = (GetSystemMetrics(0) - (CONSOLE_WIDTH * 8)) / 2;
        int y = (GetSystemMetrics(1) - (CONSOLE_HEIGHT * 16)) / 2;

        ShowWindow(handle, SW_NORMAL);
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
    }

    static void DrawBorder()
    {
        Console.Clear();
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;

        // HP Box
        Console.SetCursorPosition(6, 2);
        Console.Write("╔═══════════════╗");
        Console.SetCursorPosition(6, 3);
        Console.Write($"║   HP: {playerHP}/{maxHP}   ║");
        Console.SetCursorPosition(6, 4);
        Console.Write("╚═══════════════╝");

        // Dialog Box
        int width = dialogBoxWidth;
        int height = 10;
        int startY = 10;

        Console.SetCursorPosition(dialogBoxStartX, startY);
        Console.Write("╔" + new string('═', width - 2) + "╗");

        for (int i = 1; i < height - 1; i++)
        {
            Console.SetCursorPosition(dialogBoxStartX, startY + i);
            Console.Write("║");
            Console.SetCursorPosition(width + dialogBoxStartX - 1, startY + i);
            Console.Write("║");
        }

        Console.SetCursorPosition(dialogBoxStartX, startY + height - 1);
        Console.Write("╚" + new string('═', width - 2) + "╝");
    }

    static void RunDialog()
    {
        DrawBorder();

        string currentText = dialogues[rnd.Next(dialogues.Count)];
        string[] lines = currentText.Split('\n');
        int startY = 12;

        Console.ForegroundColor = ConsoleColor.White;
        foreach (string line in lines)
        {
            Console.SetCursorPosition(14, startY++);
            foreach (char c in line)
            {
                Console.Write(c);
                Thread.Sleep(25);
            }
        }

        DrawChoices();
        HandleInput();
    }

    static void DrawChoices()
    {
        // Draw fixed YES and NO text
        Console.ForegroundColor = ConsoleColor.White;
        Console.SetCursorPosition(yesX, choiceY);
        Console.Write("YES");
        Console.SetCursorPosition(noX, choiceY);
        Console.Write("NO");

        // Draw heart at the selected position
        Console.ForegroundColor = ConsoleColor.Red;
        int heartX = selected == 0 ? yesX + heartOffsetX : noX + heartOffsetX;
        Console.SetCursorPosition(heartX, choiceY);
        Console.Write(heart);
    }

    static void HandleInput()
    {
        bool choosing = true;
        while (choosing)
        {
            var key = Console.ReadKey(true).Key;
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                    // Clear previous heart position
                    int oldHeartX = selected == 0 ? yesX + heartOffsetX : noX + heartOffsetX;
                    Console.SetCursorPosition(oldHeartX, choiceY);
                    Console.Write(" ");

                    // Update selection and draw new heart
                    selected = 1 - selected;
                    int newHeartX = selected == 0 ? yesX + heartOffsetX : noX + heartOffsetX;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.SetCursorPosition(newHeartX, choiceY);
                    Console.Write(heart);
                    break;

                case ConsoleKey.Enter:
                    choosing = false;
                    ProcessChoice();
                    break;
            }
        }
    }

    static void ProcessChoice()
    {
        Console.SetCursorPosition(14, 17);
        Console.Write(new string(' ', 70));
        Console.SetCursorPosition(14, 18);
        Console.Write(new string(' ', 70));

        string response = selected == 0
            ? "* The water refreshes your SOUL!"
            : "* The water spirit looks disappointed...";

        Console.SetCursorPosition(14, 17);
        Console.ForegroundColor = ConsoleColor.White;
        foreach (char c in response)
        {
            Console.Write(c);
            Thread.Sleep(30);
        }

        if (selected == 0)
        {
            playerHP = Math.Min(playerHP + 5, maxHP);
        }
        else
        {
            playerHP = Math.Max(playerHP - 1, 1);
        }

        Thread.Sleep(1000);
    }
}
