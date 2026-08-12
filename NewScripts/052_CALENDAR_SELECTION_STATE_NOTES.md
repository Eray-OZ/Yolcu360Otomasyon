# 052 - Calendar Selection State Notes

Source script:

`JavaScriptCopies/052_BAService.SearchForm_WaitForCalendarSelectionStateAsync_line774.js`

Working C# method:

`Services/BrowserAutomation/BAService.SearchForm.cs`

Method:

`WaitForCalendarSelectionStateAsync(DateTime date, TimeSpan timeout)`

## What This Script Does

This script checks whether the requested date is really selected inside the
date picker after the automation clicks a day.

It does not select the date itself. It only verifies the selection state.

The script checks:

1. The date picker menu is visible.
2. The correct month/year section is found.
3. The requested day number exists inside that section.
4. The day cell has a selected state.

Selected state is detected from:

- `aria-selected="true"`
- `selected`
- `active`
- `range_start`
- `range_end`
- `dp__active_date`
- `dp__range_start`
- `dp__range_end`

## Why The Current Code Is Long

The date picker can show multiple months at the same time.

That means the same day number can exist more than once.

Example:

- August 30
- September 30

If the script only searches for `30`, it may check the wrong month.

Because of this, the current script first tries to find the calendar area that
belongs to the target month/year, then searches the day inside that area.

## Why We Should Not Aggressively Shorten It Yet

A very short version would be easier to read, but it may break date selection
verification when:

- pickup and dropoff months are different,
- two calendar panels are visible,
- the header and day grid are not inside the same direct wrapper,
- the date picker changes its internal DOM structure,
- the same day number appears in both visible months.

This area has already caused bugs before, so changing the logic without testing
would be risky.

## Safer Future Refactor

The safer refactor is not to remove the logic, but to split it into clear
functions.

Possible structure:

```javascript
(() => {
    const target = createTargetDateInfo();
    const menus = findVisibleDatePickerMenus();

    return menus.some(menu => {
        const calendar = findCalendarForTargetMonth(menu, target);
        if (!calendar) return false;

        return hasSelectedTargetDay(calendar, target.day);
    });
})();
```

Suggested helper functions:

- `normalize(value)`
- `compact(value)`
- `isVisible(element)`
- `hasTargetMonthYear(element, target)`
- `findVisibleDatePickerMenus()`
- `findCalendarForTargetMonth(menu, target)`
- `hasSelectedTargetDay(calendar, day)`

This keeps the current behavior but makes the script easier to read.

## Riskier Short Version

This kind of short version should only be used if the date picker HTML is proven
to keep the month header and day grid in the same stable wrapper:

```javascript
const monthBlock = [...document.querySelectorAll('.dp__instance_calendar')]
    .find(block => block.textContent.includes('Ağustos 2026'));

const selectedDay = [...monthBlock.querySelectorAll('.dp__cell_inner')]
    .find(cell => cell.textContent.trim() === '30');

return selectedDay?.className.includes('dp__active_date');
```

This is much easier to read but may choose the wrong day if the DOM structure
changes or if two visible months contain the same day number.

## Decision For Now

Do not change script 052 yet.

When we return to it, use the safer future refactor approach:

- keep the same behavior,
- split the logic into named helper functions,
- avoid removing multi-month calendar handling,
- test pickup/dropoff dates across different months.
