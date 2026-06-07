using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;

#if ANDROID
using Android.Content;
using Android.Views.InputMethods;
#endif

namespace BKPos.Mobile.App.Pages;

internal sealed class AppKeyboardHost : Grid
{
    private readonly View _pageContent;
    private readonly Border _keyboardPanel;
    private readonly BoxView _dismissLayer;
    private readonly VerticalStackLayout _keyRows = new() { Spacing = 3 };

    private readonly List<Entry> _entries = [];
    private Entry? _activeEntry;
    private KeyboardMode _mode = KeyboardMode.Text;
    private bool _shift;
    private bool _telexAllowed;
    private bool _telexEnabled;
    private bool _registered;

    private AppKeyboardHost(View pageContent)
    {
        _pageContent = pageContent;
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Children.Add(pageContent);
        _dismissLayer = BuildDismissLayer();
        Grid.SetRow(_dismissLayer, 0);
        Children.Add(_dismissLayer);

        _keyboardPanel = BuildKeyboardPanel();
        Grid.SetRow(_keyboardPanel, 1);
        Children.Add(_keyboardPanel);

        Loaded += (_, _) => RegisterEntries();
        Unloaded += (_, _) => _activeEntry = null;
    }

    public static View Wrap(View content) => new AppKeyboardHost(content);

    private BoxView BuildDismissLayer()
    {
        var layer = new BoxView
        {
            IsVisible = false,
            InputTransparent = false,
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.001)
        };
        AddTap(layer, HandleDismissTap);
        return layer;
    }

    private Border BuildKeyboardPanel()
    {
        return new Border
        {
            IsVisible = false,
            BackgroundColor = AppUi.Navy,
            StrokeThickness = 0,
            MaximumHeightRequest = 150,
            Padding = new Thickness(8, 5),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14, 14, 0, 0) },
            Content = _keyRows
        };
    }
    private void RegisterEntries()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        foreach (var entry in FindEntries(_pageContent).Distinct())
        {
            _entries.Add(entry);
            entry.Focused += (_, _) => ShowKeyboard(entry);
            entry.TextChanged += (_, _) => SuppressNativeKeyboard();
            entry.Unfocused += (_, _) =>
            {
                if (_activeEntry == entry && (!entry.IsEnabled || entry.IsReadOnly))
                {
                    HideKeyboard();
                }
            };
        }
    }

    private static IEnumerable<Entry> FindEntries(Element element)
    {
        if (element is Entry entry)
        {
            yield return entry;
        }

        if (element is Border border && border.Content is Element borderContent)
        {
            foreach (var child in FindEntries(borderContent))
            {
                yield return child;
            }
        }

        if (element is ContentView contentView && contentView.Content is Element content)
        {
            foreach (var child in FindEntries(content))
            {
                yield return child;
            }
        }

        if (element is ScrollView scrollView && scrollView.Content is Element scrollContent)
        {
            foreach (var child in FindEntries(scrollContent))
            {
                yield return child;
            }
        }

        if (element is Layout layout)
        {
            foreach (var child in layout.Children.OfType<Element>())
            {
                foreach (var entryChild in FindEntries(child))
                {
                    yield return entryChild;
                }
            }
        }
    }

    private void ShowKeyboard(Entry entry)
    {
        if (entry.IsReadOnly || !entry.IsEnabled)
        {
            HideKeyboard();
            return;
        }

        _activeEntry = entry;
        _mode = ResolveMode(entry);
        _telexAllowed = AllowsTelex(entry);
        _telexEnabled = _telexAllowed;
        BuildKeys();
        _keyboardPanel.IsVisible = true;
        _dismissLayer.IsVisible = true;
        SuppressNativeKeyboard();
    }

    private KeyboardMode ResolveMode(Entry entry)
    {
        var placeholder = entry.Placeholder ?? string.Empty;
        if (placeholder.Contains("192", StringComparison.OrdinalIgnoreCase)
            || placeholder.Contains("IP", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardMode.Number;
        }

        return entry.Keyboard == Keyboard.Numeric ? KeyboardMode.Number : KeyboardMode.Text;
    }

    private static bool AllowsTelex(Entry entry)
    {
        if (entry.IsPassword
            || entry.Keyboard == Keyboard.Numeric
            || entry.Keyboard == Keyboard.Telephone
            || entry.Keyboard == Keyboard.Email
            || entry.Keyboard == Keyboard.Url)
        {
            return false;
        }

        var placeholder = AppUi.NormalizeSearch(entry.Placeholder ?? string.Empty).ToLowerInvariant();
        string[] rawKeywords =
        [
            "ip",
            "192",
            "key",
            "license",
            "ban quyen",
            "id may",
            "dang nhap",
            "mat khau",
            "tien",
            "the",
            "chuyen khoan",
            "giam gia"
        ];

        return !rawKeywords.Any(placeholder.Contains);
    }

    private void HideKeyboard()
    {
        _keyboardPanel.IsVisible = false;
        _dismissLayer.IsVisible = false;
        _activeEntry?.Unfocus();
    }

    private void HandleDismissTap(TappedEventArgs args)
    {
        var tapPoint = args.GetPosition(this);
        if (tapPoint is null)
        {
            HideKeyboard();
            return;
        }

        var targetEntry = FindEntryAt(tapPoint.Value);
        if (targetEntry is null)
        {
            HideKeyboard();
            return;
        }

        _activeEntry = targetEntry;
        if (IsClearButtonTap(targetEntry, tapPoint.Value))
        {
            ClearInput();
        }

        targetEntry.Focus();
        ShowKeyboard(targetEntry);
    }

    private void BuildKeys()
    {
        _keyRows.Children.Clear();
        if (_mode == KeyboardMode.Number)
        {
            AddKeyRow(
                Key("1"), Key("2"), Key("3"), Key("4"), Key("5"),
                Key("6"), Key("7"), Key("8"), Key("9"), Key("0"),
                Action("\u232B", Backspace, 1.15));
            AddKeyRow(
                Action("ABC", () => SwitchMode(KeyboardMode.Text)),
                Key("."),
                Key(","),
                Key("00"),
                Key("000"),
                Action("Xong", HideKeyboard, 1.2));
            return;
        }

        if (_mode == KeyboardMode.Symbols)
        {
            AddKeyRow(Key("-"), Key("_"), Key("."), Key("/"), Key(":"), Key("@"), Key("#"), Key("&"), Key("+"), Key("="), Action("\u232B", Backspace, 1.15));
            AddKeyRow(Key("?"), Key("!"), Key("("), Key(")"), Key("["), Key("]"), Key("{"), Key("}"), Key("*"), Key("%"));
            AddKeyRow(
                Action("ABC", () => SwitchMode(KeyboardMode.Text), 1.0),
                Action("123", () => SwitchMode(KeyboardMode.Number), 1.0),
                Key(" ", "C\u00E1ch", 3.4),
                Action("Xong", HideKeyboard, 1.2));
            return;
        }

        AddKeyRow("qwertyuiop".Select(ch => Key(ApplyCase(ch))).Append(Action("\u232B", Backspace, 1.15)).ToArray());
        AddKeyRow([.. new[] { Spacer(0.35) }, .. "asdfghjkl".Select(ch => Key(ApplyCase(ch))), .. new[] { Spacer(0.35) }]);
        AddKeyRow(
            Action(_shift ? "shift" : "SHIFT", ToggleShift, 1.25),
            Key(ApplyCase('z')),
            Key(ApplyCase('x')),
            Key(ApplyCase('c')),
            Key(ApplyCase('v')),
            Key(ApplyCase('b')),
            Key(ApplyCase('n')),
            Key(ApplyCase('m')),
            Spacer(1.15));
        AddKeyRow(
            Action("123", () => SwitchMode(KeyboardMode.Number), 1.0),
            Action(_telexAllowed ? (_telexEnabled ? "VI" : "ABC") : "ABC", ToggleTelex, 1.0),
            Key(" ", "C\u00E1ch", 3.4),
            Action("K\u00FD t\u1EF1", () => SwitchMode(KeyboardMode.Symbols), 1.0),
            Action("Xong", HideKeyboard, 1.2));
    }
    private string ApplyCase(char value) => _shift ? char.ToUpperInvariant(value).ToString() : value.ToString();

    private void AddKeyRow(params KeyboardKey[] keys)
    {
        var row = new Grid { ColumnSpacing = 4 };
        foreach (var key in keys)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(key.Width, GridUnitType.Star)));
        }

        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            if (key.IsSpacer)
            {
                row.Add(new BoxView { Opacity = 0 }, i, 0);
                continue;
            }

                        var keyView = key.Label == "\u232B"
                ? BackspaceButton()
                : KeyButton(key.Label, key.IsAction ? AppUi.Navy2 : AppUi.SurfaceAlt, key.IsAction ? Colors.White : AppUi.Ink);
            AddTap(keyView, async () => await key.InvokeAsync());
            row.Add(keyView, i, 0);
        }

        _keyRows.Children.Add(row);
    }

    private static Border KeyButton(string text, Color background, Color textColor) => new()
    {
        BackgroundColor = background,
        StrokeThickness = 0,
        HeightRequest = 27,
        Padding = new Thickness(4, 0),
        StrokeShape = new RoundRectangle { CornerRadius = 7 },
        Content = new Label
        {
            Text = text,
            TextColor = textColor,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
    };


    private static Border BackspaceButton() => new()
    {
        BackgroundColor = AppUi.SurfaceAlt,
        Stroke = AppUi.Blue,
        StrokeThickness = 1.5,
        HeightRequest = 27,
        Padding = new Thickness(4, 0),
        StrokeShape = new RoundRectangle { CornerRadius = 7 },
        Content = new Label
        {
            Text = "\u232B",
            TextColor = AppUi.Blue,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
    };

    private static void AddTap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(tap);
    }

    private static void AddTap(View view, Action<TappedEventArgs> action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, args) => action(args);
        view.GestureRecognizers.Add(tap);
    }

    private static void AddTap(View view, Func<Task> action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await action();
        view.GestureRecognizers.Add(tap);
    }

    private KeyboardKey Key(string value, string? label = null, double width = 1)
        => new(label ?? value, width, false, () =>
        {
            Insert(value);
            return Task.CompletedTask;
        });

    private static KeyboardKey Spacer(double width)
        => new(string.Empty, width, false, () => Task.CompletedTask, true);

    private KeyboardKey Action(string label, Action action, double width = 1)
        => new(label, width, true, () =>
        {
            action();
            return Task.CompletedTask;
        });

    private KeyboardKey Action(string label, Func<Task> action, double width = 1)
        => new(label, width, true, action);

    private void Insert(string value)
    {
        if (_activeEntry is null || _activeEntry.IsReadOnly || !_activeEntry.IsEnabled)
        {
            return;
        }

        var current = _activeEntry.Text ?? string.Empty;
        var start = Math.Clamp(_activeEntry.CursorPosition, 0, current.Length);
        var length = Math.Clamp(_activeEntry.SelectionLength, 0, current.Length - start);
        var prepared = current[..start] + current[(start + length)..];
        var result = _telexEnabled && _mode == KeyboardMode.Text
            ? VietnameseTelexEngine.ProcessInsert(prepared, start, value)
            : new TelexResult(prepared[..start] + value + prepared[start..], start + value.Length);

        _activeEntry.Text = result.Text;
        _activeEntry.CursorPosition = result.CursorPosition;
        _activeEntry.SelectionLength = 0;
        SuppressNativeKeyboard();
    }
    private void Backspace()
    {
        if (_activeEntry is null || _activeEntry.IsReadOnly || !_activeEntry.IsEnabled)
        {
            return;
        }

        var current = _activeEntry.Text ?? string.Empty;
        var start = Math.Clamp(_activeEntry.CursorPosition, 0, current.Length);
        var length = Math.Clamp(_activeEntry.SelectionLength, 0, current.Length - start);
        if (length == 0 && start == 0)
        {
            return;
        }

        if (length == 0)
        {
            start--;
            length = 1;
        }

        _activeEntry.Text = current[..start] + current[(start + length)..];
        _activeEntry.CursorPosition = start;
        _activeEntry.SelectionLength = 0;
        SuppressNativeKeyboard();
    }

    private void ClearInput()
    {
        if (_activeEntry is null || _activeEntry.IsReadOnly || !_activeEntry.IsEnabled)
        {
            return;
        }

        _activeEntry.Text = string.Empty;
        _activeEntry.CursorPosition = 0;
        _activeEntry.SelectionLength = 0;
        SuppressNativeKeyboard();
    }

    private Entry? FindEntryAt(Point point)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.IsVisible || !entry.IsEnabled)
            {
                continue;
            }

            var bounds = GetBoundsInHost(entry);
            if (bounds.Contains(point))
            {
                return entry;
            }
        }

        return null;
    }

    private bool IsClearButtonTap(Entry entry, Point point)
    {
        if (string.IsNullOrEmpty(entry.Text) || entry.ClearButtonVisibility == ClearButtonVisibility.Never)
        {
            return false;
        }

        var bounds = GetBoundsInHost(entry);
        if (!bounds.Contains(point))
        {
            return false;
        }

        const double clearButtonWidth = 48d;
        return entry.FlowDirection == FlowDirection.RightToLeft
            ? point.X <= bounds.Left + clearButtonWidth
            : point.X >= bounds.Right - clearButtonWidth;
    }

    private Rect GetBoundsInHost(VisualElement element)
    {
        var x = element.X;
        var y = element.Y;
        Element? current = element.Parent;
        while (current is VisualElement parent && !ReferenceEquals(parent, this))
        {
            x += parent.X;
            y += parent.Y;
            current = parent.Parent;
        }

        return new Rect(x, y, element.Width, element.Height);
    }

    private async Task PasteAsync()
    {
        var text = await Clipboard.Default.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            Insert(text);
        }
    }

    private void ToggleShift()
    {
        _shift = !_shift;
        BuildKeys();
    }

    private void ToggleTelex()
    {
        if (!_telexAllowed)
        {
            return;
        }

        _telexEnabled = !_telexEnabled;
        BuildKeys();
    }
    private void SwitchMode(KeyboardMode mode)
    {
        _mode = mode;
        BuildKeys();
    }

    private void SuppressNativeKeyboard()
    {
#if ANDROID
        if (_activeEntry?.Handler?.PlatformView is Android.Widget.EditText editText)
        {
            editText.ShowSoftInputOnFocus = false;
            var inputMethodManager = (InputMethodManager?)editText.Context?.GetSystemService(Context.InputMethodService);
            inputMethodManager?.HideSoftInputFromWindow(editText.WindowToken, HideSoftInputFlags.None);
        }
#endif
    }

    private enum KeyboardMode
    {
        Text,
        Number,
        Symbols
    }

    private sealed record KeyboardKey(string Label, double Width, bool IsAction, Func<Task> InvokeAsync, bool IsSpacer = false);
}
