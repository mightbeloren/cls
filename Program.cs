namespace cls;

enum Mode { Normal, PendingDelete, Cut, Copy, Search }

class Program
{
    static int cursor = 0;
    static string[] files = [];
    static string currentDir = Directory.GetCurrentDirectory();
    static HashSet<int> selectedLines = [];
    static Mode currentMode = Mode.Normal;
    static int pendingLine = -1;
    static List<string> clipboard = [];
    static Mode clipboardOp = Mode.Normal;
    static bool renamingMode = false;
    static string renameBuffer = "";
    static int scrollOffset = 0;
    static string numberBuffer = "";
    static bool pendingG = false;
    static string searchBuffer = "";
    static int[] searchMatches = [];
    static int searchMatchIndex = 0;

    static void Main(string[] args)
    {
        Console.Clear();
        Console.CursorVisible = false;
        LoadFiles();

        while (true)
        {
            Render();
            var key = Console.ReadKey(true);
            HandleKey(key);
        }
    }

    static void LoadFiles()
    {
        var dirs = Directory.GetDirectories(currentDir).Select(d => d + "/");
        var fs = Directory.GetFiles(currentDir);
        files = dirs.Concat(fs).ToArray();
        if (cursor >= files.Length) cursor = Math.Max(0, files.Length - 1);
    }

    static void Render()
    {
        Console.Clear();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(currentDir);
        Console.ResetColor();
        Console.WriteLine();

        int listHeight = Console.WindowHeight - 4;
        int visibleEnd = Math.Min(scrollOffset + listHeight, files.Length);

        for (int i = scrollOffset; i < visibleEnd; i++)
        {
            string name = Path.GetFileName(files[i].TrimEnd('/')) + (files[i].EndsWith("/") ? "/" : "");
            bool isSelected = selectedLines.Contains(i);
            bool isCursor = i == cursor;
            bool isPending = currentMode == Mode.PendingDelete && (isSelected || (selectedLines.Count == 0 && i == pendingLine));
            bool isCutCopy = (currentMode == Mode.Cut || currentMode == Mode.Copy) && (isSelected || (selectedLines.Count == 0 && i == pendingLine));
            bool isSearchMatch = searchMatches.Contains(i);

            if (isCursor)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
            }
            else if (isPending)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (isCutCopy)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else if (isSelected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            else if (isSearchMatch)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            string meta = "";
            if (files[i].EndsWith("/"))
            {
                try
                {
                    int count = Directory.GetFileSystemEntries(files[i].TrimEnd('/')).Length;
                    meta = $"{count} items";
                }
                catch { meta = "?"; }
            }
            else
            {
                try
                {
                    long bytes = new FileInfo(files[i]).Length;
                    meta = bytes switch
                    {
                        < 1024 => $"{bytes}B",
                        < 1024 * 1024 => $"{bytes / 1024}K",
                        < 1024L * 1024 * 1024 => $"{bytes / (1024 * 1024)}M",
                        _ => $"{bytes / (1024L * 1024 * 1024)}G"
                    };
                }
                catch { meta = "?"; }
            }

            if (renamingMode && isCursor)
                Console.WriteLine(("  > " + renameBuffer).PadRight(Console.WindowWidth));
            else
                Console.WriteLine(($"  {i + 1,3}  {name,-40} {meta,8}").PadRight(Console.WindowWidth));

            Console.ResetColor();
        }

        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        if (currentMode == Mode.Search)
            Console.Write($"/{searchBuffer}_");
        else if (numberBuffer.Length > 0)
            Console.Write($":{numberBuffer}");
        else if (pendingG)
            Console.Write("g");
        Console.ResetColor();
    }

    static void HandleKey(ConsoleKeyInfo key)
    {
        if (renamingMode)
        {
            HandleRenameInput(key);
            return;
        }

        if (currentMode == Mode.Search)
        {
            HandleSearchInput(key);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Escape:
                Reset();
                break;

            case ConsoleKey.Tab:
                if (selectedLines.Contains(cursor))
                    selectedLines.Remove(cursor);
                else
                    selectedLines.Add(cursor);
                break;

            case ConsoleKey.Enter:
                if (numberBuffer.Length > 0)
                {
                    if (int.TryParse(numberBuffer, out int line))
                    {
                        int target = Math.Clamp(line - 1, 0, files.Length - 1);
                        JumpTo(target);
                    }
                    numberBuffer = "";
                }
                else
                {
                    OpenOrEnter();
                }
                break;

            default:
                HandleCharKey(key);
                break;
        }
    }

    static void HandleCharKey(ConsoleKeyInfo key)
    {
        int listHeight = Console.WindowHeight - 4;
        char c = key.KeyChar;

        if (char.IsDigit(c) && currentMode == Mode.Normal)
        {
            numberBuffer += c;
            pendingG = false;
            return;
        }

        if (c == '/')
        {
            currentMode = Mode.Search;
            searchBuffer = "";
            searchMatches = [];
            return;
        }

        if (c == 'n' && searchMatches.Length > 0)
        {
            searchMatchIndex = (searchMatchIndex + 1) % searchMatches.Length;
            JumpTo(searchMatches[searchMatchIndex]);
            return;
        }

        if (c == 'N' && searchMatches.Length > 0)
        {
            searchMatchIndex = (searchMatchIndex - 1 + searchMatches.Length) % searchMatches.Length;
            JumpTo(searchMatches[searchMatchIndex]);
            return;
        }

        if (c == 'g')
        {
            if (pendingG)
            {
                JumpTo(0);
                pendingG = false;
                numberBuffer = "";
            }
            else
            {
                pendingG = true;
                numberBuffer = "";
            }
            return;
        }

        if (c == 'G')
        {
            if (numberBuffer.Length > 0 && int.TryParse(numberBuffer, out int line))
                JumpTo(Math.Clamp(line - 1, 0, files.Length - 1));
            else
                JumpTo(files.Length - 1);
            numberBuffer = "";
            pendingG = false;
            return;
        }

        pendingG = false;
        numberBuffer = "";

        switch (c)
        {
            case 'j':
                if (cursor < files.Length - 1)
                {
                    cursor++;
                    if (cursor >= scrollOffset + listHeight) scrollOffset++;
                }
                break;

            case 'k':
                if (cursor > 0)
                {
                    cursor--;
                    if (cursor < scrollOffset) scrollOffset--;
                }
                break;

            case 'd':
                if (currentMode == Mode.PendingDelete)
                {
                    var targets = GetTargets();
                    foreach (var path in targets)
                    {
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);
                        else if (File.Exists(path))
                            File.Delete(path);
                    }
                    Reset();
                    LoadFiles();
                }
                else if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.PendingDelete;
                    pendingLine = cursor;
                }
                break;

            case 'x':
                if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.Cut;
                    pendingLine = cursor;
                    clipboard = GetTargets();
                    clipboardOp = Mode.Cut;
                }
                break;

            case 'c':
                if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.Copy;
                    pendingLine = cursor;
                    clipboard = GetTargets();
                    clipboardOp = Mode.Copy;
                }
                break;

            case 'p':
                if (clipboard.Count > 0)
                {
                    foreach (var src in clipboard)
                    {
                        string name = Path.GetFileName(src.TrimEnd('/'));
                        string dest = Path.Combine(currentDir, name);
                        if (clipboardOp == Mode.Cut)
                        {
                            if (Directory.Exists(src))
                                Directory.Move(src, dest);
                            else if (File.Exists(src))
                                File.Move(src, dest);
                        }
                        else if (clipboardOp == Mode.Copy)
                        {
                            if (Directory.Exists(src))
                                CopyDirectory(src, dest);
                            else if (File.Exists(src))
                                File.Copy(src, dest, overwrite: false);
                        }
                    }
                    clipboard.Clear();
                    clipboardOp = Mode.Normal;
                    currentMode = Mode.Normal;
                    selectedLines.Clear();
                    LoadFiles();
                }
                break;

            case 'r':
                if (files.Length > 0)
                {
                    renamingMode = true;
                    renameBuffer = Path.GetFileName(files[cursor].TrimEnd('/'));
                }
                break;

            case '.':
                var parent = Directory.GetParent(currentDir)?.FullName;
                if (parent != null)
                {
                    currentDir = parent;
                    cursor = 0;
                    scrollOffset = 0;
                    selectedLines.Clear();
                    LoadFiles();
                }
                break;

            case 'q':
                Console.CursorVisible = true;
                Console.Clear();
                Environment.Exit(0);
                break;
        }
    }

    static void HandleSearchInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            currentMode = Mode.Normal;
            searchBuffer = "";
            searchMatches = [];
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            currentMode = Mode.Normal;
            if (searchMatches.Length > 0)
            {
                searchMatchIndex = 0;
                JumpTo(searchMatches[0]);
            }
            return;
        }

        if (key.Key == ConsoleKey.Backspace && searchBuffer.Length > 0)
        {
            searchBuffer = searchBuffer[..^1];
        }
        else if (!char.IsControl(key.KeyChar))
        {
            searchBuffer += key.KeyChar;
        }

        if (searchBuffer.Length > 0)
        {
            searchMatches = files
                .Select((f, i) => (name: Path.GetFileName(f.TrimEnd('/')), i))
                .Where(x => x.name.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.i)
                .ToArray();

            if (searchMatches.Length > 0)
            {
                searchMatchIndex = 0;
                JumpTo(searchMatches[0]);
            }
        }
        else
        {
            searchMatches = [];
        }
    }

    static void HandleRenameInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            renamingMode = false;
            renameBuffer = "";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            string oldPath = files[cursor].TrimEnd('/');
            string newPath = Path.Combine(currentDir, renameBuffer);
            if (oldPath != newPath)
            {
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
                else if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
            }
            renamingMode = false;
            renameBuffer = "";
            LoadFiles();
            return;
        }

        if (key.Key == ConsoleKey.Backspace && renameBuffer.Length > 0)
        {
            renameBuffer = renameBuffer[..^1];
            return;
        }

        if (!char.IsControl(key.KeyChar))
            renameBuffer += key.KeyChar;
    }

    static void JumpTo(int index)
    {
        int listHeight = Console.WindowHeight - 4;
        cursor = index;
        if (cursor < scrollOffset)
            scrollOffset = cursor;
        else if (cursor >= scrollOffset + listHeight)
            scrollOffset = cursor - listHeight + 1;
    }

    static void OpenOrEnter()
    {
        if (files.Length == 0) return;
        string path = files[cursor].TrimEnd('/');

        if (Directory.Exists(path))
        {
            currentDir = path;
            cursor = 0;
            scrollOffset = 0;
            selectedLines.Clear();
            currentMode = Mode.Normal;
            LoadFiles();
        }
        else if (File.Exists(path))
        {
            Console.CursorVisible = true;
            Console.Clear();
            System.Diagnostics.Process.Start("xdg-open", path);
            Console.CursorVisible = false;
        }
    }

    static List<string> GetTargets()
    {
        if (selectedLines.Count > 0)
            return selectedLines.Select(i => files[i].TrimEnd('/')).ToList();
        return new List<string> { files[cursor].TrimEnd('/') };
    }

    static void Reset()
    {
        currentMode = Mode.Normal;
        selectedLines.Clear();
        pendingLine = -1;
        renamingMode = false;
        renameBuffer = "";
        scrollOffset = 0;
        numberBuffer = "";
        pendingG = false;
        searchBuffer = "";
        searchMatches = [];
    }

    static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
