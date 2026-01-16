# Status Card Implementation Plan

## Overview
Create a draggable, semi-transparent status card panel in the top-right corner of the chart that displays key bot information with show/hide functionality.

---

## 1. Requirements Analysis

### Display Information
- **Account**: User-defined account name from settings
- **Risk Mode**: Fixed or Dynamic
- **Listening Port**: Webhook port number (only in Auto mode)
- **Port Status**: OK or Error indicator (only in Auto mode)
- **Bot Mode**: Auto or Manual_Only

### UI Requirements
- **Position**: Top-right corner of chart
- **Draggable**: User can move the card anywhere on screen
- **Semi-transparent**: Background with opacity for visibility
- **Show/Hide**: Toggle button on bottom trading panel
- **Persistent**: Remember position after dragging (if possible)

### Settings
- **Status Card Visibility**: Displayed / Hidden (default: Displayed)
- **Position**: After Bot Mode but before Account parameter

---

## 2. Technical Implementation Strategy

### 2.1 cTrader UI Capabilities Research

**Known cTrader API Features:**
- `Border` control supports drag-and-drop via mouse events
- `Opacity` property available on controls (0.0 to 1.0)
- `Chart.AddControl()` for adding UI elements
- Mouse events: `MouseDown`, `MouseMove`, `MouseUp`
- Position via `HorizontalAlignment` and `VerticalAlignment`
- `Margin` for precise positioning

**Draggable Implementation:**
- Use `Border` as container with mouse event handlers
- Track mouse position on `MouseDown`
- Update `Margin` on `MouseMove` when dragging
- Release on `MouseUp`
- Store drag offset to maintain smooth dragging

**Limitations:**
- No built-in drag-and-drop component
- Must implement custom drag logic
- Position persistence requires storing coordinates (can use class-level variables)

---

## 3. Code Structure

### 3.1 New Enum
```csharp
public enum StatusCardVisibility
{
    Displayed,
    Hidden
}
```

### 3.2 New Parameter (after Bot Mode, before Account)
```csharp
[Parameter("Status Card", DefaultValue = StatusCardVisibility.Displayed)]
public StatusCardVisibility StatusCardVisibility { get; set; }
```

### 3.3 New Class: StatusCard
```csharp
public class StatusCard : CustomControl
{
    // Properties
    private Robot _bot;
    private Border _mainBorder;
    private StackPanel _contentPanel;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Thickness _dragStartMargin;
    
    // Status fields
    private TextBlock _accountText;
    private TextBlock _riskModeText;
    private TextBlock _portText;
    private TextBlock _portStatusText;
    private TextBlock _botModeText;
    
    // Constructor
    public StatusCard(Robot bot, parameters...)
    
    // Methods
    private ControlBase CreateCard()
    private void UpdateStatus()
    private void OnMouseDown(MouseButtonEventArgs args)
    private void OnMouseMove(MouseEventArgs args)
    private void OnMouseUp(MouseButtonEventArgs args)
    public void Show()
    public void Hide()
    public void RefreshPortStatus()
}
```

### 3.4 Main Robot Class Changes

**New Fields:**
```csharp
private StatusCard _statusCard;
private bool _webhookServerError = false;
```

**Modified Methods:**
- `OnStart()`: Initialize status card, add to chart
- `WebhookListenerCallback()`: Update port status on errors
- Webhook validation: Track connection status

---

## 4. Detailed Implementation Steps

### Step 1: Add Enum and Parameter
**Location**: After `BotModeType` enum (line ~115)
```csharp
public enum StatusCardVisibility
{
    Displayed,
    Hidden
}
```

**Location**: After `BotMode` parameter (line ~287)
```csharp
[Parameter("Status Card", DefaultValue = StatusCardVisibility.Displayed)]
public StatusCardVisibility StatusCardVisibility { get; set; }
```

### Step 2: Create StatusCard Class
**Location**: After `Styles` class, before `Robot` class (line ~275)

**Key Components:**
1. **Card Container**: Border with semi-transparent background
2. **Header**: "Bot Status" title with drag handle indicator
3. **Content**: StackPanel with status rows
4. **Styling**: Match existing panel style with opacity

**Layout Structure:**
```
Border (draggable, semi-transparent)
└── StackPanel (vertical)
    ├── TextBlock (header: "Bot Status")
    ├── Separator line
    ├── TextBlock (Account: {value})
    ├── TextBlock (Risk Mode: {value})
    ├── TextBlock (Listening Port: {value}) [conditional]
    ├── TextBlock (Port Status: {value}) [conditional]
    └── TextBlock (Bot Mode: {value})
```

### Step 3: Implement Dragging Logic

**Mouse Event Handlers:**
```csharp
private void OnMouseDown(MouseButtonEventArgs args)
{
    _isDragging = true;
    _dragStartPoint = new Point(args.XAxis, args.YAxis);
    _dragStartMargin = _mainBorder.Margin;
}

private void OnMouseMove(MouseEventArgs args)
{
    if (!_isDragging) return;
    
    double deltaX = args.XAxis - _dragStartPoint.X;
    double deltaY = args.YAxis - _dragStartPoint.Y;
    
    _mainBorder.Margin = new Thickness(
        _dragStartMargin.Left + deltaX,
        _dragStartMargin.Top + deltaY,
        _dragStartMargin.Right - deltaX,
        _dragStartMargin.Bottom - deltaY
    );
}

private void OnMouseUp(MouseButtonEventArgs args)
{
    _isDragging = false;
}
```

### Step 4: Style Implementation

**Semi-transparent Background:**
```csharp
public static Style CreateStatusCardStyle()
{
    var style = new Style();
    style.Set(ControlProperty.CornerRadius, 5);
    style.Set(ControlProperty.BackgroundColor, Color.FromHex("#292929").WithAlpha(0.85), ControlState.DarkTheme);
    style.Set(ControlProperty.BackgroundColor, Color.FromHex("#FFFFFF").WithAlpha(0.90), ControlState.LightTheme);
    style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
    style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
    style.Set(ControlProperty.BorderThickness, new Thickness(1));
    style.Set(ControlProperty.Padding, new Thickness(10));
    return style;
}
```

**Note**: If `WithAlpha()` doesn't exist, use `Color.FromArgb(alpha, r, g, b)` instead.

### Step 5: Add Toggle Button to Trading Panel

**Location**: In `TradingPanel.CreatePanel()` method (line ~1806)

**Implementation:**
```csharp
// Add after branding text or at end of panel
var statusToggleButton = new Button
{
    Text = "Status",
    Style = Styles.CreateLightGreyButtonStyle(),
    Margin = "10 0 0 0",
    Width = 60
};
statusToggleButton.Click += (args) => _bot.ToggleStatusCard();
main.AddChild(statusToggleButton);
```

### Step 6: Conditional Display Logic

**In StatusCard.UpdateStatus():**
```csharp
private void UpdateStatus()
{
    _accountText.Text = $"Account: {_accountName}";
    _riskModeText.Text = $"Risk Mode: {_riskMode}";
    _botModeText.Text = $"Bot Mode: {_botMode}";
    
    // Conditional display based on Bot Mode
    if (_botMode == BotModeType.Auto)
    {
        _portText.IsVisible = true;
        _portStatusText.IsVisible = true;
        _portText.Text = $"Listening Port: {_webhookPort}";
        _portStatusText.Text = _webhookServerError 
            ? "Port Status: ⚠️ Error" 
            : "Port Status: ✓ OK";
        _portStatusText.ForegroundColor = _webhookServerError 
            ? Color.Red 
            : Color.Green;
    }
    else
    {
        _portText.IsVisible = false;
        _portStatusText.IsVisible = false;
    }
}
```

### Step 7: Port Status Monitoring

**Track webhook server status:**

**In OnStart():**
```csharp
if (WebhookEnabled && BotMode == BotModeType.Auto)
{
    try
    {
        _webhookListener = new HttpListener();
        _webhookListener.Prefixes.Add($"http://localhost:{WebhookPort}/webhook/");
        _webhookListener.Start();
        _webhookListener.BeginGetContext(WebhookListenerCallback, _webhookListener);
        _webhookServerError = false; // Success
        PrintLocal($"[WEBHOOK-SERVER] Started successfully on port {WebhookPort}");
    }
    catch (Exception ex)
    {
        _webhookServerError = true; // Error
        PrintLocal($"[WEBHOOK-SERVER] Failed to start: {ex.Message}");
    }
}
```

**Update status card after initialization:**
```csharp
if (_statusCard != null)
    _statusCard.RefreshPortStatus();
```

### Step 8: Integration with Main Robot

**In OnStart() method:**
```csharp
// After trading panel initialization
if (StatusCardVisibility == StatusCardVisibility.Displayed)
{
    _statusCard = new StatusCard(
        this,
        AccountName,
        RiskMode,
        WebhookPort,
        BotMode,
        _webhookServerError
    );
    
    var statusBorder = new Border
    {
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = "0 20 20 0"
    };
    statusBorder.Child = _statusCard;
    Chart.AddControl(statusBorder);
}
```

**Add toggle method:**
```csharp
public void ToggleStatusCard()
{
    if (_statusCard == null) return;
    
    if (_statusCard.IsVisible)
        _statusCard.Hide();
    else
        _statusCard.Show();
}
```

---

## 5. Testing Checklist

### Functionality Tests
- [ ] Status card appears in top-right when Displayed is selected
- [ ] Status card hidden when Hidden is selected
- [ ] All information displays correctly
- [ ] Port and Port Status only show in Auto mode
- [ ] Port and Port Status hidden in Manual Only mode
- [ ] Status button toggles visibility correctly

### Dragging Tests
- [ ] Card can be dragged with mouse
- [ ] Card follows mouse smoothly during drag
- [ ] Card stays in new position after drag
- [ ] Drag works in all chart areas
- [ ] No performance issues during drag

### Visual Tests
- [ ] Background is semi-transparent
- [ ] Text is readable on all chart backgrounds
- [ ] Styling matches existing panels
- [ ] Border and corners render correctly
- [ ] Colors work in both light/dark themes

### Edge Cases
- [ ] Card doesn't go off-screen during drag
- [ ] Toggle button works when card is dragged
- [ ] Status updates when bot mode changes
- [ ] Port status updates on webhook errors
- [ ] Card respects initial visibility setting

---

## 6. Implementation Order

1. **Add enum and parameter** (5 min)
2. **Create StatusCard class skeleton** (10 min)
3. **Implement basic UI layout** (15 min)
4. **Add styling with opacity** (10 min)
5. **Implement drag functionality** (20 min)
6. **Add conditional display logic** (10 min)
7. **Integrate with main robot** (15 min)
8. **Add toggle button to trading panel** (10 min)
9. **Implement port status monitoring** (10 min)
10. **Test all functionality** (20 min)

**Total Estimated Time**: ~2 hours

---

## 7. Potential Issues & Solutions

### Issue 1: Opacity Not Working
**Solution**: Use `Color.FromArgb(alpha, r, g, b)` instead of `WithAlpha()`
```csharp
// Dark theme: 85% opacity
Color.FromArgb(217, 41, 41, 41) // 217 = 0.85 * 255
```

### Issue 2: Dragging Not Smooth
**Solution**: 
- Ensure mouse events are properly subscribed
- Use `BeginInvokeOnMainThread()` if needed
- Consider throttling updates

### Issue 3: Card Goes Off-Screen
**Solution**: Add boundary checks in `OnMouseMove()`
```csharp
// Clamp position to chart bounds
double maxLeft = Chart.Width - _mainBorder.Width;
double maxTop = Chart.Height - _mainBorder.Height;
// Apply clamping logic
```

### Issue 4: Status Not Updating
**Solution**: 
- Call `UpdateStatus()` in `OnTick()` periodically
- Or use event-driven updates when values change
- Add refresh method for manual updates

### Issue 5: Toggle Button Not Working
**Solution**: 
- Ensure `_statusCard` reference is accessible
- Check if `IsVisible` property exists, otherwise use `Opacity = 0`
- Store visibility state in class field

---

## 8. Code Organization

### Files Modified
- `SkyAnalyst Automated Trader.cs` (main file)

### New Code Sections
1. **Enums** (line ~115): Add `StatusCardVisibility`
2. **Styles** (line ~197): Add `CreateStatusCardStyle()`
3. **StatusCard Class** (line ~275): New class before Robot
4. **Robot Parameters** (line ~287): Add status card parameter
5. **Robot Fields** (line ~430): Add `_statusCard` and `_webhookServerError`
6. **OnStart()** (line ~466): Initialize and add status card
7. **TradingPanel** (line ~1806): Add toggle button
8. **New Methods**: `ToggleStatusCard()`, port status tracking

---

## 9. Alternative Approaches

### Approach A: Simple Static Card (Easier)
- No dragging functionality
- Fixed position top-right
- Simpler implementation
- Less code to maintain

### Approach B: Collapsible Card (Medium)
- Click header to collapse/expand
- No dragging needed
- Shows minimal info when collapsed
- Better for small screens

### Approach C: Full Draggable (Recommended)
- Complete drag functionality
- Position persistence
- Professional appearance
- Best user experience

---

## 10. Future Enhancements

### Phase 2 Features
- [ ] Save card position to file (persist across restarts)
- [ ] Add minimize/maximize button
- [ ] Add refresh button for manual status update
- [ ] Color-code risk mode (green=safe, yellow=warning, red=danger)
- [ ] Add daily P&L to status card
- [ ] Add active positions count
- [ ] Animated transitions for show/hide
- [ ] Resize handle for card dimensions

### Phase 3 Features
- [ ] Multiple status cards for different info
- [ ] Customizable fields (user selects what to show)
- [ ] Export status to file/screenshot
- [ ] Status history log viewer
- [ ] Alert notifications on status card

---

## 11. Summary

This implementation provides a professional, draggable status card that:
- ✅ Displays key bot information at a glance
- ✅ Adapts display based on Bot Mode (Auto/Manual)
- ✅ Can be moved anywhere on screen
- ✅ Has semi-transparent background for chart visibility
- ✅ Includes show/hide toggle for user control
- ✅ Monitors webhook port status in real-time
- ✅ Matches existing UI styling and theme support
- ✅ Minimal performance impact

The draggable functionality enhances user experience by allowing traders to position the card where it doesn't interfere with their chart analysis while keeping critical bot status information visible.
