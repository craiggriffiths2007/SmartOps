# SmartOps web styles

Use four spaces for indentation and put each CSS declaration on its own line.
Keep selectors at the same indentation level unless they are inside an at-rule
such as `@media`. Write short comments for sections or non-obvious behaviour.

## Where to make a change

| File | Responsibility |
| --- | --- |
| `site.css` | Theme variables, page defaults, buttons and shared print visibility |
| `navigation.css` | Desktop navigation and role-aware mobile menus |
| `pages/navigation-responsive.css` | General mobile layout and navigation overrides |
| `components.css` | Shared tables, grids, lightboxes and Quill defaults |
| `view-components.css` | Reusable presentation classes used by views and partials |
| `diary.css` | Shared day, week and month calendar styles |
| `home.css` | Public home content, galleries and public mobile menu |
| `item-report.css` | Survey reports and their print layout |
| `company.css` | Company theme picker |
| `cookie-consent.css` | Cookie banner |
| `dynamic-questions.css` | Question editor; loaded by its page |
| `pages/*.css` | Styles for a specific page or partial; the opening comment identifies the view |
| `pages/print-document.css` | Shared standalone A4 layout for Signed Mandate, Garage Motor and Timber Colours |
| `pages/customer-privacy.css` | Standalone customer privacy HTML page |

`Views/Shared/_Layout.cshtml.css` is ASP.NET scoped CSS. It is included through
the generated `SmartOps.Web.styles.css`; keep its rules separate from global CSS.

## Loading and overrides

The shared layout loads the global styles in a fixed order. `view-components.css`
comes after the generated scoped stylesheet. Page stylesheet links replace the
old style blocks at their original locations to preserve the cascade. The general
mobile overrides are deliberately linked at the end of the shared layout.

Razor stylesheet links use `asp-append-version="true"` to invalidate cached CSS.
The standalone privacy HTML page uses a relative link because it has no Razor
tag helpers. Standalone print pages load only their own document stylesheet.

Prefer an existing named class when the same appearance is needed in another
view. Add reusable presentation to `view-components.css`, component behaviour to
its existing stylesheet, and page-only rules to the matching file in `pages/`.
Keep responsive rules with the component they affect and preserve selector
specificity when simplifying overrides.

The remaining inline theme colours are computed by Razor. Initial visibility for
the recent-survey panel and profile-icon fallback stays inline because JavaScript
updates it. Do not replace JavaScript-managed visibility with Bootstrap `d-none`,
which uses `!important` and would prevent `element.style.display` from showing it.

Third-party CSS in `wwwroot/lib`, and generated files in `bin` and `obj`, are not
application style sources and should not be edited as part of a CSS cleanup.

## Checking changes

Run `dotnet build SmartOps.Web.csproj --no-restore` to check Razor compilation.
For style changes, check desktop and narrow mobile layouts, theme colours, diary
expansion, profile fallbacks, editors and printing. Browser declaration/computed
style comparisons can verify refactors; they do not replace checking complete
authenticated pages with real application data.
