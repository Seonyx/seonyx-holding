# Seonyx Holdings - Branding Guidelines

## Logo

### Current Logo Analysis

The existing logo is a low-resolution pinwheel design featuring:
- **Primary colors**: Purple/Violet and Green
- **Design**: Four-quadrant pinwheel/windmill shape
- **Style**: Modern, geometric, symmetrical
- **Format**: Currently PNG (low-res), needs regeneration

### Logo Recreation Specifications

**SVG Logo Structure:**
The logo should be recreated as an SVG with the following specifications:

```svg
<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
  <!-- Four triangular sections forming a pinwheel -->
  
  <!-- Top-left triangle (Purple) -->
  <path d="M 50 50 L 0 0 L 50 0 Z" fill="#7C3AED" />
  
  <!-- Top-right triangle (Green) -->
  <path d="M 50 50 L 100 0 L 100 50 Z" fill="#10B981" />
  
  <!-- Bottom-right triangle (Purple) -->
  <path d="M 50 50 L 100 100 L 50 100 Z" fill="#7C3AED" />
  
  <!-- Bottom-left triangle (Green) -->
  <path d="M 50 50 L 0 100 L 0 50 Z" fill="#10B981" />
</svg>
```

### Logo Variations

**Primary Logo** - Full color
- Use on white or light backgrounds
- Purple (#7C3AED) and Green (#10B981)

**Reversed Logo** - Light on dark
- Use on dark backgrounds
- Lighter purple (#A78BFA) and lighter green (#34D399)

**Monochrome Logo** - Single color
- Use when color printing is not available
- Black (#000000) or White (#FFFFFF)

**Logo + Wordmark**
```
[Logo] SEONYX
       Holdings
```
- Logo on left, wordmark on right
- Wordmark in: Inter, Helvetica Neue, or sans-serif
- "SEONYX" in uppercase, 24px, weight 700
- "Holdings" in sentence case, 14px, weight 400

### Logo Sizes and Usage

**Website:**
- Header/Navigation: 48px × 48px
- Favicon: 32px × 32px, 16px × 16px
- Large hero: 120px × 120px
- Footer: 32px × 32px

**Print:**
- Minimum size: 20mm × 20mm
- Recommended: 40mm × 40mm

**Clear Space:**
Maintain clear space around logo equal to 1/4 of the logo's height on all sides.

### File Formats Needed

1. **logo.svg** - Vector format (primary)
2. **logo.png** - High-res PNG (300dpi)
   - 512px × 512px (web)
   - 2048px × 2048px (print)
3. **logo@2x.png** - Retina display
   - 96px × 96px at 2x (for 48px display)
4. **favicon.ico** - Browser favicon
   - 16px, 32px, 48px sizes embedded
5. **logo-reversed.svg** - Light version for dark backgrounds
6. **logo-monochrome.svg** - Single color version

## Color Palette

### Primary Colors

**Purple (Brand Primary)**
- Hex: `#7C3AED`
- RGB: `rgb(124, 58, 237)`
- HSL: `hsl(262, 83%, 58%)`
- Usage: Primary buttons, links, headlines, logo

**Green (Brand Secondary)**
- Hex: `#10B981`
- RGB: `rgb(16, 185, 129)`
- HSL: `hsl(158, 84%, 39%)`
- Usage: Accents, CTAs, success states, logo

### Extended Palette

**Purple Shades:**
- Purple 50: `#FAF5FF` (backgrounds)
- Purple 100: `#F3E8FF` (subtle backgrounds)
- Purple 200: `#E9D5FF`
- Purple 300: `#D8B4FE`
- Purple 400: `#C084FC`
- Purple 500: `#A855F7`
- Purple 600: `#7C3AED` (primary)
- Purple 700: `#6D28D9` (hover state)
- Purple 800: `#5B21B6` (dark elements)
- Purple 900: `#4C1D95` (text on light)

**Green Shades:**
- Green 50: `#F0FDF4` (backgrounds)
- Green 100: `#DCFCE7` (subtle backgrounds)
- Green 200: `#BBF7D0`
- Green 300: `#86EFAC`
- Green 400: `#4ADE80`
- Green 500: `#22C55E`
- Green 600: `#10B981` (primary)
- Green 700: `#059669` (hover state)
- Green 800: `#047857` (dark elements)
- Green 900: `#065F46` (text on light)

### Neutral Colors

**Grays:**
- Gray 50: `#F9FAFB` (backgrounds)
- Gray 100: `#F3F4F6` (subtle backgrounds)
- Gray 200: `#E5E7EB` (borders)
- Gray 300: `#D1D5DB` (disabled elements)
- Gray 400: `#9CA3AF` (placeholder text)
- Gray 500: `#6B7280` (secondary text)
- Gray 600: `#4B5563` (body text)
- Gray 700: `#374151` (headings)
- Gray 800: `#1F2937` (dark backgrounds)
- Gray 900: `#111827` (darkest backgrounds)

**Black and White:**
- Pure Black: `#000000`
- Pure White: `#FFFFFF`

### Division-Specific Colors

**Techwrite** - Purple theme
- Primary: `#7C3AED` (Purple 600)
- Background: `#FAF5FF` (Purple 50)

**Literary Agency** - Green theme
- Primary: `#10B981` (Green 600)
- Background: `#F0FDF4` (Green 50)

**Inglesolar** - Amber (solar/energy)
- Primary: `#F59E0B` (Amber 500)
- Background: `#FFFBEB` (Amber 50)

**Pixtracta** - Blue (technology)
- Primary: `#3B82F6` (Blue 500)
- Background: `#EFF6FF` (Blue 50)

**Homesonthemed** - Red/Rose (Mediterranean)
- Primary: `#EF4444` (Red 500)
- Background: `#FEF2F2` (Red 50)

### Color Usage Guidelines

**Text:**
- Primary text: Gray 700 `#374151`
- Secondary text: Gray 500 `#6B7280`
- Disabled text: Gray 400 `#9CA3AF`
- Link text: Purple 600 `#7C3AED`
- Link hover: Purple 700 `#6D28D9`

**Backgrounds:**
- Page background: White `#FFFFFF`
- Section background: Gray 50 `#F9FAFB`
- Card background: White `#FFFFFF`
- Footer background: Gray 900 `#111827`

**Borders:**
- Default border: Gray 200 `#E5E7EB`
- Hover border: Gray 300 `#D1D5DB`
- Focus border: Purple 600 `#7C3AED`

**Buttons:**
- Primary button: Purple 600 background `#7C3AED`
- Primary button hover: Purple 700 `#6D28D9`
- Primary button text: White `#FFFFFF`
- Secondary button: Gray 100 background `#F3F4F6`
- Secondary button hover: Gray 200 `#E5E7EB`
- Secondary button text: Gray 700 `#374151`

**States:**
- Success: Green 600 `#10B981`
- Warning: Amber 500 `#F59E0B`
- Error: Red 500 `#EF4444`
- Info: Blue 500 `#3B82F6`

## Typography

### Font Families

**Primary Font Stack:**
```css
font-family: 'Inter', 'Helvetica Neue', 'Segoe UI', 'Roboto', 'Arial', sans-serif;
```

**Headings:**
```css
font-family: 'Inter', 'Helvetica Neue', 'Arial', sans-serif;
font-weight: 700; /* Bold */
```

**Body Text:**
```css
font-family: 'Inter', 'Helvetica Neue', 'Arial', sans-serif;
font-weight: 400; /* Regular */
```

**Monospace (code):**
```css
font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
```

### Type Scale

**Display:**
- Display 1: 64px / 4rem (line-height: 1.1)
- Display 2: 48px / 3rem (line-height: 1.2)

**Headings:**
- H1: 36px / 2.25rem (line-height: 1.25)
- H2: 30px / 1.875rem (line-height: 1.3)
- H3: 24px / 1.5rem (line-height: 1.4)
- H4: 20px / 1.25rem (line-height: 1.5)
- H5: 18px / 1.125rem (line-height: 1.5)
- H6: 16px / 1rem (line-height: 1.5)

**Body:**
- Large: 18px / 1.125rem (line-height: 1.75)
- Normal: 16px / 1rem (line-height: 1.6)
- Small: 14px / 0.875rem (line-height: 1.5)
- Extra Small: 12px / 0.75rem (line-height: 1.4)

### Font Weights

- Light: 300 (sparingly)
- Regular: 400 (body text)
- Medium: 500 (emphasis)
- Semibold: 600 (subheadings)
- Bold: 700 (headings, buttons)
- Extra Bold: 800 (display text only)

## Spacing System

Use a consistent 8px base unit:

- 0: 0px
- 1: 8px
- 2: 16px
- 3: 24px
- 4: 32px
- 5: 40px
- 6: 48px
- 8: 64px
- 10: 80px
- 12: 96px
- 16: 128px
- 20: 160px
- 24: 192px

**Common Usage:**
- Small gap: 8px
- Medium gap: 16px
- Large gap: 24px
- Section spacing: 48px
- Hero padding: 64px

## Layout

### Container Widths

- Mobile: 100% (with 16px padding)
- Tablet: 768px max-width
- Desktop: 1024px max-width
- Wide: 1280px max-width

### Breakpoints

```css
/* Mobile first approach */
@media (min-width: 640px) { /* Small tablets */ }
@media (min-width: 768px) { /* Tablets */ }
@media (min-width: 1024px) { /* Desktop */ }
@media (min-width: 1280px) { /* Large desktop */ }
```

### Grid System

**12-column grid** with the following common layouts:
- 1 column (mobile)
- 2 columns (tablet)
- 3 columns (desktop)
- 4 columns (wide desktop)

## UI Components

### Buttons

**Primary Button:**
```css
background: #7C3AED;
color: #FFFFFF;
padding: 12px 24px;
border-radius: 8px;
font-weight: 600;
border: none;
transition: background 0.2s;
```

**Primary Button Hover:**
```css
background: #6D28D9;
```

**Secondary Button:**
```css
background: #F3F4F6;
color: #374151;
padding: 12px 24px;
border-radius: 8px;
font-weight: 600;
border: 1px solid #E5E7EB;
```

### Cards

```css
background: #FFFFFF;
border: 1px solid #E5E7EB;
border-radius: 12px;
padding: 24px;
box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
```

### Forms

**Input Fields:**
```css
border: 1px solid #E5E7EB;
border-radius: 8px;
padding: 10px 14px;
font-size: 16px;
background: #FFFFFF;
```

**Input Focus:**
```css
border-color: #7C3AED;
outline: 2px solid rgba(124, 58, 237, 0.2);
outline-offset: 2px;
```

### Navigation

**Header:**
- Background: White
- Height: 64px
- Border bottom: 1px solid Gray 200
- Logo: 48px

**Footer:**
- Background: Gray 900
- Text: Gray 300
- Links: Gray 400 (hover: White)
- Padding: 48px

## Icons

**Icon Library:** Heroicons or similar
**Icon Sizes:**
- Small: 16px
- Medium: 20px
- Large: 24px
- Extra Large: 32px

**Icon Color:** Match text color or use brand colors for emphasis

## Accessibility

### Color Contrast

All text must meet WCAG AA standards:
- Normal text (< 18px): 4.5:1 contrast ratio
- Large text (≥ 18px): 3:1 contrast ratio

**Verified Combinations:**
- Purple 600 on White: ✓ Pass
- Green 600 on White: ✓ Pass
- White on Gray 900: ✓ Pass
- Gray 700 on White: ✓ Pass

### Focus States

All interactive elements must have visible focus indicators:
```css
:focus {
  outline: 2px solid #7C3AED;
  outline-offset: 2px;
}
```

## CSS Variables

Define brand colors as CSS custom properties:

```css
:root {
  /* Brand Colors */
  --color-brand-purple: #7C3AED;
  --color-brand-green: #10B981;
  
  /* Grays */
  --color-gray-50: #F9FAFB;
  --color-gray-100: #F3F4F6;
  --color-gray-200: #E5E7EB;
  --color-gray-300: #D1D5DB;
  --color-gray-400: #9CA3AF;
  --color-gray-500: #6B7280;
  --color-gray-600: #4B5563;
  --color-gray-700: #374151;
  --color-gray-800: #1F2937;
  --color-gray-900: #111827;
  
  /* Spacing */
  --spacing-xs: 8px;
  --spacing-sm: 16px;
  --spacing-md: 24px;
  --spacing-lg: 32px;
  --spacing-xl: 48px;
  
  /* Border Radius */
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-xl: 16px;
  
  /* Shadows */
  --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
  --shadow-md: 0 1px 3px rgba(0, 0, 0, 0.1);
  --shadow-lg: 0 4px 6px rgba(0, 0, 0, 0.1);
}
```

## Logo Implementation Tasks for Claude Code

1. **Create SVG logo** (`Content/images/logo.svg`)
   - Use exact pinwheel design
   - Purple #7C3AED and Green #10B981
   - Viewbox: 0 0 100 100

2. **Export PNG versions**
   - 512×512px at 300dpi
   - 96×96px for retina @2x
   - Optimize file size

3. **Create favicon**
   - Multi-size .ico file
   - 16px, 32px, 48px versions

4. **Create reversed logo**
   - Light purple #A78BFA
   - Light green #34D399
   - For use on dark backgrounds

This completes the branding specifications for the Seonyx Holdings website.
