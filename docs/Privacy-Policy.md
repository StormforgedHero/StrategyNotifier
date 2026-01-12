# Privacy Policy page (static site)

The public privacy policy is served as a static page alongside the site assets. Its source lives at `src/Frontend/Gem.Frontend/privacy.html` and is published via GitHub Pages.

Key points:
- The page is bilingual (EN/PL) with a shared language switch that persists selection via `sn_language` in `localStorage` (also respects `?lang=`).
- No contact section is embedded; unsubscribe instructions are included in every email footer. The footer text is configured via the Apps Script `UNSUBSCRIBE_CONTACT` property (not hard-coded in the page).
- Links between the homepage and privacy page are relative (`./privacy.html`) for GitHub Pages compatibility under a project path.

If the repository path or hosting changes, update the privacy page link in the footer and the `PRIVACY_POLICY_URL` script property to match the deployed URL.
