using System.Runtime.InteropServices;

namespace JaneERP
{
    /// <summary>
    /// Jvnction Jane theme — white/light-gray content area · dark-teal header + sidebar · teal + purple accents.
    /// </summary>
    public static class Theme
    {
        // ── Borderless / drag support ─────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, nint wParam, nint lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION        = 0x2;

        public static void MakeBorderless(Form form)
        {
            form.FormBorderStyle = FormBorderStyle.None;
            form.MouseDown += FormDrag;
        }

        public static void MakeDraggable(Form form, Control target)
        {
            if (target is Button) return;
            target.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;

                const int bw = 8;
                var posInForm = form.PointToClient(Cursor.Position);
                if (posInForm.X <= bw || posInForm.X >= form.ClientSize.Width  - bw ||
                    posInForm.Y <= bw || posInForm.Y >= form.ClientSize.Height - bw)
                    return;

                ReleaseCapture();
                SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            };
            foreach (Control child in target.Controls)
                MakeDraggable(form, child);
        }

        private static void FormDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || sender is not Form f) return;

            const int bw = 8;
            var p = f.PointToClient(Cursor.Position);
            if (p.X <= bw || p.X >= f.ClientSize.Width  - bw ||
                p.Y <= bw || p.Y >= f.ClientSize.Height - bw)
                return;

            ReleaseCapture();
            SendMessage(f.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        public static Button AddCloseButton(Form form)
        {
            var btn = new Button
            {
                Text      = "×",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                Size      = new Size(32, 28),
                BackColor = Color.FromArgb(180, 40, 30),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                TabStop   = false
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Danger;
            btn.Click += (_, _) => form.Close();
            form.Controls.Add(btn);

            void Position()
            {
                btn.Location = new Point(form.ClientSize.Width - btn.Width - 2, 2);
                btn.BringToFront();
            }

            form.Load   += (_, _) => Position();
            form.Resize += (_, _) => Position();
            Position();
            return btn;
        }

        // ── Borderless resize support ─────────────────────────────────────────────
        private class ResizableHelper : NativeWindow
        {
            private const int WM_NCHITTEST   = 0x0084;
            private const int WM_SETCURSOR   = 0x0020;
            private const int HTLEFT         = 10, HTRIGHT       = 11;
            private const int HTTOP          = 12, HTTOPLEFT     = 13, HTTOPRIGHT    = 14;
            private const int HTBOTTOM       = 15, HTBOTTOMLEFT  = 16, HTBOTTOMRIGHT = 17;
            private const int BorderWidth    = 8;
            private readonly Form _form;

            internal ResizableHelper(Form form)
            {
                _form = form;
                AssignHandle(form.Handle);
                form.Disposed += (_, _) => ReleaseHandle();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    var p = _form.PointToClient(Cursor.Position);
                    int w = _form.ClientSize.Width, h = _form.ClientSize.Height;
                    bool l = p.X <= BorderWidth, r = p.X >= w - BorderWidth;
                    bool t = p.Y <= BorderWidth, b = p.Y >= h - BorderWidth;
                    if (t && l)  { m.Result = (IntPtr)HTTOPLEFT;     return; }
                    if (t && r)  { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                    if (b && l)  { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                    if (b && r)  { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    if (l)       { m.Result = (IntPtr)HTLEFT;        return; }
                    if (r)       { m.Result = (IntPtr)HTRIGHT;       return; }
                    if (t)       { m.Result = (IntPtr)HTTOP;         return; }
                    if (b)       { m.Result = (IntPtr)HTBOTTOM;      return; }
                }

                if (m.Msg == WM_SETCURSOR)
                {
                    int htCode = (int)(m.LParam.ToInt64() & 0xFFFF);
                    Cursor? cursor = htCode switch
                    {
                        HTLEFT or HTRIGHT                   => Cursors.SizeWE,
                        HTTOP  or HTBOTTOM                  => Cursors.SizeNS,
                        HTTOPLEFT  or HTBOTTOMRIGHT         => Cursors.SizeNWSE,
                        HTTOPRIGHT or HTBOTTOMLEFT          => Cursors.SizeNESW,
                        _                                   => null
                    };
                    if (cursor != null)
                    {
                        Cursor.Current = cursor;
                        m.Result       = (IntPtr)1;
                        return;
                    }
                }

                base.WndProc(ref m);
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, ResizableHelper>
            _resizeHelpers = new();

        public static void MakeResizable(Form form)
        {
            void Attach()
            {
                var helper = new ResizableHelper(form);
                _resizeHelpers.AddOrUpdate(form, helper);
            }
            if (form.IsHandleCreated) Attach();
            else form.HandleCreated += (_, _) => Attach();
        }

        // ── Core palette ─────────────────────────────────────────────────────────
        // Light theme: off-white content · dark-teal header/sidebar · teal primary · purple secondary
        public static readonly Color Background    = Color.FromArgb(243, 246, 249);  // #F3F6F9 — content bg
        public static readonly Color Surface       = Color.FromArgb(255, 255, 255);  // #FFFFFF — card/panel
        public static readonly Color Header        = Color.FromArgb(11,  37,  42);   // #0B252A — dark teal
        public static Color Gold          = Color.FromArgb(0,   190, 214);  // #00BED6 — teal primary
        public static Color GoldDark      = Color.FromArgb(0,   145, 165);  // #0091A5 — deep teal
        public static Color Teal          = Color.FromArgb(109, 40,  217);  // #6D28D9 — purple secondary
        public static readonly Color TextPrimary   = Color.FromArgb(26,  32,  44);   // #1A202C — near-black
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);  // #64748B — medium gray
        public static readonly Color TextMuted     = Color.FromArgb(148, 163, 184);  // #94A3B8 — muted gray
        public static readonly Color Border        = Color.FromArgb(226, 232, 240);  // #E2E8F0 — light border
        public static readonly Color InputBg       = Color.FromArgb(248, 250, 252);  // #F8FAFC — input bg
        public static readonly Color RowAlt        = Color.FromArgb(248, 250, 252);  // #F8FAFC — alt row
        public static readonly Color Selected      = Color.FromArgb(0,   190, 214);  // teal — selection bg
        public static readonly Color Danger        = Color.FromArgb(220, 38,  38);   // #DC2626 — red

        /// <summary>Semi-transparent teal used for hover/active borders on tiles.</summary>
        public static Color GlowOuter = Color.FromArgb(90, 0, 190, 214);

        /// <summary>
        /// Applies custom accent/highlight colours loaded from <paramref name="settings"/>.
        /// Call once at startup, after <see cref="AppSettings.Load"/>, before any form is shown.
        /// </summary>
        public static void ApplyCustomColors(AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.AccentColor))
            {
                try
                {
                    var c  = ColorTranslator.FromHtml(settings.AccentColor);
                    Gold      = c;
                    GoldDark  = Color.FromArgb(
                        Math.Max(0, c.R - 30),
                        Math.Max(0, c.G - 45),
                        Math.Max(0, c.B - 50));
                    GlowOuter = Color.FromArgb(90, c.R, c.G, c.B);
                }
                catch { /* keep defaults on parse failure */ }
            }
            if (!string.IsNullOrEmpty(settings.HighlightColor))
            {
                try { Teal = ColorTranslator.FromHtml(settings.HighlightColor); }
                catch { }
            }
        }

        // ── Dark-panel detector ───────────────────────────────────────────────────
        /// <summary>Returns true if the control lives inside a panel tagged "header" or "sidebar".</summary>
        private static bool IsOnDarkPanel(Control c)
        {
            var p = c.Parent;
            while (p != null)
            {
                if (p.Tag as string is "header" or "sidebar") return true;
                p = p.Parent;
            }
            return false;
        }

        /// <summary>Recursively apply the theme to a control and all its children.</summary>
        public static void Apply(Control root)
        {
            ApplyOne(root);
            foreach (Control c in root.Controls)
                Apply(c);
        }

        private static void ApplyOne(Control c)
        {
            switch (c)
            {
                case Form f:
                    f.BackColor = Background;
                    f.ForeColor = TextPrimary;
                    break;

                case TabPage tp:
                    tp.BackColor = Background;
                    tp.ForeColor = TextPrimary;
                    break;

                case Panel p:
                {
                    var ptag = p.Tag as string;
                    if (ptag is "header" or "sidebar")
                    {
                        p.BackColor = Header;
                        p.ForeColor = Color.White;
                    }
                    else if (ptag == "card")
                    {
                        p.BackColor = Surface;       // white card
                        p.ForeColor = TextPrimary;
                    }
                    else if (ptag == null)           // untagged → standard content bg
                    {
                        p.BackColor = Background;
                        p.ForeColor = TextPrimary;
                    }
                    // else: custom tag → leave colours intact (accent lines, dividers, etc.)
                    break;
                }

                case GroupBox grp:
                    grp.BackColor = Surface;
                    grp.ForeColor = TextSecondary;
                    break;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    lbl.ForeColor = IsOnDarkPanel(lbl) ? Color.White : TextPrimary;
                    break;

                case TextBox txt:
                    txt.BackColor   = InputBg;
                    txt.ForeColor   = TextPrimary;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case RichTextBox rtb:
                    rtb.BackColor   = Surface;
                    rtb.ForeColor   = TextPrimary;
                    rtb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case NumericUpDown nud:
                    nud.BackColor = InputBg;
                    nud.ForeColor = TextPrimary;
                    break;

                case Button btn:
                    if (btn.UseVisualStyleBackColor)
                        StyleButton(btn);
                    break;

                case DataGridView dgv:
                    StyleGrid(dgv);
                    break;

                case ComboBox cbo:
                    cbo.BackColor = InputBg;
                    cbo.ForeColor = TextPrimary;
                    cbo.FlatStyle = FlatStyle.Flat;
                    break;

                case ListBox lst:
                    lst.BackColor = InputBg;
                    lst.ForeColor = TextPrimary;
                    break;

                case CheckBox chk:
                    chk.BackColor = Color.Transparent;
                    chk.ForeColor = IsOnDarkPanel(chk) ? Color.White : TextPrimary;
                    break;

                case RadioButton rb:
                    rb.BackColor = Color.Transparent;
                    rb.ForeColor = IsOnDarkPanel(rb) ? Color.White : TextPrimary;
                    break;

                case DateTimePicker dtp:
                    dtp.BackColor               = InputBg;
                    dtp.ForeColor               = TextPrimary;
                    dtp.CalendarForeColor       = TextPrimary;
                    dtp.CalendarMonthBackground = Surface;
                    dtp.CalendarTitleBackColor  = Gold;
                    dtp.CalendarTitleForeColor  = Color.White;
                    break;

                case TabControl tc:
                    tc.BackColor = Background;
                    tc.ForeColor = TextPrimary;
                    break;

                case PictureBox:
                    break;  // leave as-is

                default:
                    if (!IsOnDarkPanel(c))
                    {
                        c.BackColor = Background;
                        c.ForeColor = TextPrimary;
                    }
                    break;
            }
        }

        /// <summary>Teal-filled primary action button.</summary>
        public static void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor        = Gold;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = GoldDark;
            btn.BackColor = Gold;
            btn.ForeColor = Color.White;
            btn.Cursor    = Cursors.Hand;
        }

        /// <summary>Outlined secondary button (subtle border, no fill).</summary>
        public static void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor        = Border;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 244, 248);
            btn.BackColor = Surface;
            btn.ForeColor = TextPrimary;
            btn.Cursor    = Cursors.Hand;
        }

        /// <summary>Apply light theme to a DataGridView.</summary>
        public static void StyleGrid(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.BackgroundColor           = InputBg;
            dgv.ForeColor                 = TextPrimary;
            dgv.GridColor                 = Border;
            dgv.BorderStyle               = BorderStyle.None;

            dgv.DefaultCellStyle.BackColor          = Surface;
            dgv.DefaultCellStyle.ForeColor          = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = Selected;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Header;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersBorderStyle               = DataGridViewHeaderBorderStyle.Single;
        }

        /// <summary>Path to the Jane mascot image.</summary>
        public static string MascotImagePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "jane_mascot.jpg");

        /// <summary>Default path to the horizontal Jvnction logo.</summary>
        public static string DefaultLogoPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo_horizontal.png");

        public static PictureBox CreateLogoBadge(int x, int y, int width = 160, int height = 44)
        {
            var pb = new PictureBox
            {
                Location  = new Point(x, y),
                Size      = new Size(width, height),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Padding   = new Padding(4),
                Cursor    = Cursors.Default
            };

            try
            {
                var path = AppSettings.Current.LogoPath;
                if (File.Exists(path))
                    pb.Image = Image.FromFile(path);
                else if (File.Exists(DefaultLogoPath))
                    pb.Image = Image.FromFile(DefaultLogoPath);
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[Theme.CreateLogoBadge]: {ex.Message}"); }

            return pb;
        }
    }
}
