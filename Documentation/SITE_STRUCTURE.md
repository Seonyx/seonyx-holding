# Site Structure and Navigation

## Site Map

```
Seonyx Holdings (Home)
├── About
├── Divisions
│   ├── Techwrite
│   │   ├── Overview
│   │   ├── Services
│   │   └── Portfolio
│   ├── Literary Agency
│   │   ├── Overview
│   │   ├── Authors
│   │   │   └── Maureen Avis (individual author page)
│   │   └── Submissions
│   ├── Inglesolar
│   │   ├── Overview
│   │   ├── Services
│   │   └── Contact
│   ├── Pixtracta
│   │   ├── Overview
│   │   ├── Technology
│   │   └── Features
│   └── Homesonthemed
│       ├── Overview
│       └── Coming Soon
└── Contact

Admin Area (not in public navigation)
└── /admin
    ├── Login
    ├── Dashboard
    ├── Pages
    │   ├── List
    │   ├── Create
    │   └── Edit/{id}
    ├── Divisions
    ├── Authors
    │   ├── List
    │   ├── Create
    │   └── Edit/{id}
    ├── Books
    │   ├── List
    │   ├── Create
    │   └── Edit/{id}
    ├── Content Blocks
    ├── Contact Submissions
    └── Settings
```

## URL Structure

### Public URLs
- `GET /` - Homepage
- `GET /about` - About page
- `GET /contact` - Contact form
- `POST /contact` - Submit contact form
- `GET /{division-slug}` - Division overview (e.g., `/techwrite`, `/literary-agency`)
- `GET /{division-slug}/{page-slug}` - Division sub-pages (e.g., `/techwrite/services`)
- `GET /literary-agency/authors/{author-slug}` - Individual author page
- `GET /literary-agency/books/{book-slug}` - Individual book page (future)

### Admin URLs
- `GET /admin` - Redirect to login or dashboard
- `GET /admin/login` - Login page
- `POST /admin/login` - Process login
- `GET /admin/logout` - Logout
- `GET /admin/dashboard` - Admin home
- `GET /admin/pages` - List all pages
- `GET /admin/pages/create` - Create new page
- `GET /admin/pages/edit/{id}` - Edit page
- `POST /admin/pages/save` - Save page
- `DELETE /admin/pages/delete/{id}` - Delete page
- Similar patterns for authors, books, divisions, etc.

## Navigation Design

### Primary Navigation (Top)
- Seonyx Logo (links to home)
- About
- Divisions (dropdown)
  - Techwrite
  - Literary Agency
  - Inglesolar
  - Pixtracta
  - Homesonthemed
- Contact

### Division Dropdown
Dynamically generated from Divisions table, sorted by SortOrder.

### Secondary Navigation (Division Pages)
When viewing a division page, show secondary nav with that division's pages:
- Overview (default)
- [Other pages for that division]

### Breadcrumbs
Show current location:
- Home > Divisions > Techwrite > Services
- Home > Literary Agency > Authors > Maureen Avis

### Footer Navigation
- About
- Contact
- Privacy Policy (future)
- Admin Login (small link)

## Page Templates

### 1. Homepage Template
**Layout:** `Views/Shared/_Layout.cshtml`
**View:** `Views/Home/Index.cshtml`

**Content Sections:**
- Hero section (from ContentBlocks: `homepage-hero`)
- Divisions showcase (cards/grid from Divisions table)
- Call-to-action
- Footer

### 2. Standard Content Page Template
**View:** `Views/Page/Index.cshtml`

**Content Sections:**
- Breadcrumbs
- Page Title (H1)
- Page Content (from Pages.Content - rendered as HTML)
- Sidebar (optional - related pages)

### 3. Division Overview Template
**View:** `Views/Division/Index.cshtml`

**Content Sections:**
- Division name and description
- Division logo/branding
- List of division pages (sub-navigation)
- Call-to-action button (e.g., "Visit Techwrite.online")

### 4. Author Profile Template
**View:** `Views/LiteraryAgency/Author.cshtml`

**Content Sections:**
- Author photo
- Pen name and biography
- Genre
- List of books (with covers, synopses, Amazon links)

### 5. Contact Page Template
**View:** `Views/Contact/Index.cshtml`

**Content Sections:**
- Contact form
  - Name (required)
  - Email (required)
  - Subject (optional)
  - Message (required)
  - Anti-spam measure (honeypot or simple CAPTCHA)
- Contact information (if any)

### 6. Admin Dashboard Template
**Layout:** `Views/Shared/_AdminLayout.cshtml`
**View:** `Views/Admin/Dashboard.cshtml`

**Content Sections:**
- Summary stats
  - Total pages
  - Unread contact submissions
  - Total authors
  - Total books
- Quick actions
  - Create new page
  - View contact submissions
  - Logout

### 7. Admin Form Templates
**Views:** `Views/Admin/Pages/`, `Views/Admin/Authors/`, etc.

**Standard CRUD Forms:**
- List view (table with edit/delete actions)
- Create view (form)
- Edit view (form with current values)

## Content Organization

### Division-Specific Content
Each division gets its own folder structure in the database:
- Parent page: Division overview (e.g., `literary-agency`)
- Child pages: Sub-pages (e.g., `literary-agency/authors`)

### Reusable Content
ContentBlocks table stores snippets used across multiple pages:
- Homepage hero text
- Footer content
- Call-to-action buttons
- About blurbs

## Responsive Design

### Desktop (> 992px)
- Full navigation bar with dropdown
- Multi-column layouts
- Sidebar on content pages

### Tablet (768px - 992px)
- Condensed navigation
- 2-column grids
- Sidebar below content

### Mobile (< 768px)
- Hamburger menu
- Single column layout
- Stacked navigation
- Touch-friendly buttons

## Content Management Features

### WYSIWYG Editor
Use a simple rich text editor for page content:
- TinyMCE (lightweight, works in older browsers)
- Or CKEditor
- Stored as HTML in database

### Image Uploads
- Upload to `/Content/images/uploads/`
- Reference in content via relative URLs
- Admin interface for uploading

### Draft vs Published
- Pages can be saved as drafts (IsPublished = 0)
- Preview drafts in admin area
- Publish when ready

## SEO Considerations

### Meta Tags
Each page includes:
- Title tag (from Pages.Title)
- Meta description (from Pages.MetaDescription)
- Meta keywords (from Pages.MetaKeywords - optional)
- Open Graph tags for social sharing

### URLs
- Clean, readable slugs (no IDs)
- Hierarchical structure
- Canonical URLs

### Sitemap
- Generate XML sitemap at `/sitemap.xml`
- List all published pages
- Include last modified dates

### Robots.txt
```
User-agent: *
Disallow: /admin/
Sitemap: https://seonyx.com/sitemap.xml
```

## Sample Content Structure

### Homepage
```
Title: Seonyx Holdings
Slug: home
Content: [Hero section from ContentBlock] + [Divisions grid]
```

### About Page
```
Title: About Seonyx Holdings
Slug: about
Content: 
- Company history (established 2010)
- Vision and mission
- Overview of divisions
```

### Techwrite Overview
```
Title: Techwrite
Slug: techwrite
Parent: null (top-level division)
Division: Techwrite
Content:
- Description of non-fiction writing services
- Link to techwrite.online
- Portfolio highlights
```

### Literary Agency - Authors
```
Title: Our Authors
Slug: literary-agency/authors
Parent: literary-agency
Division: Literary Agency
Content:
- Introduction to the author stable
- [List of authors from Authors table]
```

### Maureen Avis Author Page
```
Dynamic route: /literary-agency/authors/maureen-avis
Generated from Authors table entry
Content:
- Biography
- Genre: Science Fiction
- Books (from Books table where AuthorId = Maureen's ID)
  - Title, synopsis, cover, Amazon link
```

## Navigation Helper Requirements

C# helper methods needed:

```csharp
// Get primary navigation items
List<Page> GetPrimaryNavigation()

// Get division dropdown items
List<Division> GetActiveDivisions()

// Get breadcrumb trail for current page
List<Page> GetBreadcrumbs(int pageId)

// Get child pages for a division
List<Page> GetDivisionPages(int divisionId)
```

## Admin Area Features

### Page Management
- List all pages in tree view (show hierarchy)
- Create/Edit/Delete pages
- Toggle Published status
- Reorder pages (drag-drop or up/down arrows)
- Preview before publishing

### Author Management
- List all authors
- Create/Edit/Delete authors
- Upload author photos
- Reorder authors in stable

### Book Management
- List all books grouped by author
- Create/Edit/Delete books
- Upload cover images
- Add Amazon/KDP links

### Contact Submissions
- List all submissions (newest first)
- Mark as read/unread
- Flag as spam
- Delete submissions

### Content Blocks
- List all blocks
- Edit block content
- Preview blocks

### Site Settings
- Edit site-wide settings
- Update admin password
- Configure SMTP for emails
- Update CAPTCHA keys

## Future Enhancements (Not Phase 1)

- Blog functionality for divisions
- Photo galleries
- Document library
- Newsletter signup
- Multi-language support
- Full search functionality
