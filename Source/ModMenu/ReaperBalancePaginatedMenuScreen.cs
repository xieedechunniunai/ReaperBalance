using System.Collections.Generic;
using System.Linq;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Screens;
using UnityEngine;

namespace ReaperBalance.Source.ModMenu;

/// <summary>
/// A custom paginated menu screen that keeps reset button in ControlsPane.
/// Global toggle is added to content pages for better layout.
/// </summary>
public class ReaperBalancePaginatedMenuScreen : AbstractMenuScreen
{
    private readonly List<INavigableMenuEntity> _pages = new();

    private readonly IntRangeChoiceModel _pageNumberModel;
    private readonly ChoiceElement<int> _pageNumberElement;

    private readonly SelectableElement _resetButtonElement;

    /// <summary>
    /// Top anchor point for all pages.
    /// </summary>
    public Vector2 Anchor = new Vector2(0, 300);

    /// <summary>
    /// Construct a ReaperBalancePaginatedMenuScreen with the given title and reset button.
    /// Global toggle is now added to content pages instead of ControlsPane for better layout.
    /// </summary>
    public ReaperBalancePaginatedMenuScreen(
        string title,
        SelectableElement resetButton)
        : base(title)
    {
        _resetButtonElement = resetButton;

        // Page number model - will be updated when pages are added
        _pageNumberModel = new IntRangeChoiceModel(0, 0, 0) { Circular = true, DisplayFn = i => (i + 1).ToString() };
        _pageNumberModel.OnValueChanged += _ => UpdateLayout();

        _pageNumberElement = new ChoiceElement<int>("Page", _pageNumberModel);
        _pageNumberElement.SetGameObjectParent(ControlsPane);

        // Add reset button to ControlsPane (global toggle moved to content pages)
        _resetButtonElement.SetGameObjectParent(ControlsPane);

        // Position elements in ControlsPane
        // Layout: PageNumber -> Reset -> Back
        var backButtonRect = BackButton.GetComponent<RectTransform>();
        var pos = backButtonRect.anchoredPosition;
        float vspace = SpacingConstants.VSPACE_MEDIUM;

        // Match anchor settings to BackButton before setting anchoredPosition
        // BackButton uses anchor [0.5, 0.5] (center), but ChoiceElement/TextButton default to [0, 0] or [0, 1]
        var centerAnchor = new Vector2(0.5f, 0.5f);
        _pageNumberElement.RectTransform.anchorMin = centerAnchor;
        _pageNumberElement.RectTransform.anchorMax = centerAnchor;
        _resetButtonElement.RectTransform.anchorMin = centerAnchor;
        _resetButtonElement.RectTransform.anchorMax = centerAnchor;

        // Layout from top to bottom: PageNumber -> Reset -> Back
        // PageNumber at BackButton's original position
        _pageNumberElement.RectTransform.anchoredPosition = pos;
        pos.y -= vspace;

        // Reset button below PageNumber
        _resetButtonElement.RectTransform.anchoredPosition = pos;
        pos.y -= vspace;

        // Move BackButton to the bottom
        backButtonRect.anchoredPosition = pos;

        UpdateLayout();
    }

    /// <summary>
    /// The currently selected page number, 0-based.
    /// </summary>
    public int PageNumber
    {
        get => _pageNumberModel.Value;
        set => _pageNumberModel.Value = value;
    }

    /// <summary>
    /// The number of pages on this menu screen.
    /// </summary>
    public int PageCount => _pages.Count;

    /// <inheritdoc/>
    protected override IEnumerable<MenuElement> AllElements() =>
        _pages.SelectMany(p => p.AllElements())
            .Concat(new MenuElement[] { _pageNumberElement, _resetButtonElement });

    /// <summary>
    /// Add a singular page to the list of pages.
    /// </summary>
    public void AddPage(INavigableMenuEntity page)
    {
        _pages.Add(page);
        page.SetGameObjectParent(ContentPane);
    }

    /// <summary>
    /// Add multiple pages to the list of pages.
    /// </summary>
    public void AddPages(IEnumerable<INavigableMenuEntity> pages)
    {
        foreach (var page in pages)
            AddPage(page);
    }

    private INavigableMenuEntity? ActivePage => _pages.Count > 0 ? _pages[PageNumber] : null;

    /// <inheritdoc/>
    protected override SelectableElement? GetDefaultSelectableInternal() =>
        ActivePage?.GetDefaultSelectable();

    /// <inheritdoc/>
    protected override void UpdateLayout()
    {
        if (_pages.Count == 0)
            return; // Nothing to update.

        // Update the page number model.
        _pageNumberModel.ResetParams(0, _pages.Count - 1, _pageNumberElement.Value);
        _pageNumberElement.Visibility.VisibleSelf = _pages.Count > 1;

        // Set the active page.
        for (int i = 0; i < _pages.Count; i++)
            _pages[i].Visibility.VisibleSelf = i == PageNumber;

        // Set spacing for the active page.
        ActivePage?.UpdateLayout(Anchor);

        // Build navigation column: ActivePage -> PageNumber (if visible) -> Reset -> Back
        var column = new List<INavigable>();
        if (ActivePage != null)
            column.Add(ActivePage);
        if (_pages.Count > 1)
            column.Add(_pageNumberElement);
        column.Add(_resetButtonElement);
        column.Add(new SelectableWrapper(BackButton));

        // Clear all neighbors first
        foreach (var nav in column)
            nav.ClearNeighbors();

        // Connect navigation circularly
        for (int i = 0; i < column.Count; i++)
        {
            var current = column[i];
            var next = column[(i + 1) % column.Count];

            if (current.GetSelectable(NavigationDirection.Down, out var downSel))
                next.SetNeighbor(NavigationDirection.Up, downSel);
            if (next.GetSelectable(NavigationDirection.Up, out var upSel))
                current.SetNeighbor(NavigationDirection.Down, upSel);
        }
    }

    /// <summary>
    /// Internal wrapper for Selectable to implement INavigable.
    /// </summary>
    private class SelectableWrapper : INavigable
    {
        private readonly UnityEngine.UI.Selectable _button;

        public SelectableWrapper(UnityEngine.UI.Selectable button)
        {
            _button = button;
        }

        public void ClearNeighbors()
        {
            var nav = _button.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnUp = null;
            nav.selectOnDown = null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            _button.navigation = nav;
        }

        public void ClearNeighbor(NavigationDirection direction)
        {
            var nav = _button.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            switch (direction)
            {
                case NavigationDirection.Up:
                    nav.selectOnUp = null;
                    break;
                case NavigationDirection.Down:
                    nav.selectOnDown = null;
                    break;
                case NavigationDirection.Left:
                    nav.selectOnLeft = null;
                    break;
                case NavigationDirection.Right:
                    nav.selectOnRight = null;
                    break;
            }
            _button.navigation = nav;
        }

        public void SetNeighbor(NavigationDirection direction, UnityEngine.UI.Selectable selectable)
        {
            var nav = _button.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            switch (direction)
            {
                case NavigationDirection.Up:
                    nav.selectOnUp = selectable;
                    break;
                case NavigationDirection.Down:
                    nav.selectOnDown = selectable;
                    break;
                case NavigationDirection.Left:
                    nav.selectOnLeft = selectable;
                    break;
                case NavigationDirection.Right:
                    nav.selectOnRight = selectable;
                    break;
            }
            _button.navigation = nav;
        }

        public bool GetSelectable(NavigationDirection direction, out UnityEngine.UI.Selectable selectable)
        {
            selectable = _button;
            return true;
        }
    }
}
