-- ============================================
-- Seonyx Holdings Seed Data
-- Run after schema.sql on a fresh database
-- ============================================

-- Insert Divisions
INSERT INTO Divisions (Name, Slug, Description, WebsiteUrl, SortOrder, BackgroundColor, ForegroundColor) VALUES
('Techwrite', 'techwrite', 'Non-fiction writing and editorial services', 'https://techwrite.online', 1, '#6B46C1', '#FFFFFF'),
('Literary Agency', 'literary-agency', 'Representing science fiction authors', NULL, 2, '#059669', '#FFFFFF'),
('Inglesolar', 'inglesolar', 'Solar energy consultancy for Southern Spain', NULL, 3, '#F59E0B', '#000000'),
('Pixtracta', 'pixtracta', 'AI-powered real estate software with image recognition', NULL, 4, '#3B82F6', '#FFFFFF'),
('Homesonthemed', 'homesonthemed', 'Mediterranean property listings', NULL, 5, '#EF4444', '#FFFFFF');

-- Insert Home Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder, ShowInNavigation) VALUES
('home', 'Seonyx Holdings', 'Seonyx Holdings - A diversified holding company with divisions in publishing, renewable energy, and technology',
'<h1>Welcome to Seonyx Holdings</h1><p>A diversified holding company with expertise across multiple sectors including publishing, renewable energy, and technology.</p>',
0, 0);

-- Insert About Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder) VALUES
('about', 'About Seonyx Holdings', 'Learn about Seonyx Holdings and our diverse portfolio of businesses',
'<h1>About Seonyx Holdings</h1>
<p>Established in 2010, Seonyx has evolved from its origins as an SEO services company into a dynamic holding company managing diverse business ventures across multiple sectors.</p>
<h2>Our Story</h2>
<p>The name Seonyx was originally coined with SEO embedded in its name for search visibility. Over the years, the company has grown beyond its original scope to encompass a portfolio of businesses spanning publishing, renewable energy, real estate technology, and property services.</p>
<h2>Our Vision</h2>
<p>We believe in building tomorrow''s businesses today. Each division under the Seonyx umbrella operates with entrepreneurial spirit while benefiting from the shared resources and expertise of the group.</p>',
1);

-- Insert Contact Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder) VALUES
('contact', 'Contact Us', 'Get in touch with Seonyx Holdings',
'<h1>Contact Us</h1><p>We would love to hear from you. Please use the form below to get in touch.</p>',
99);

-- Insert Maureen Avis as initial author
INSERT INTO Authors (PenName, Biography, Genre, SortOrder) VALUES
('Maureen Avis',
'Maureen Avis is an up-and-coming science fiction author with a unique voice in speculative fiction. Her work explores the intersection of technology and humanity, set against richly imagined futures.',
'Science Fiction',
1);

-- Insert Content Blocks
INSERT INTO ContentBlocks (BlockKey, Title, Content) VALUES
('homepage-hero', 'Homepage Hero', '<h1>Seonyx Holdings</h1><p class="lead">Building tomorrow''s businesses today</p>'),
('footer-text', 'Footer Content', '<p>&copy; 2025 Seonyx Holdings. All rights reserved.</p>');

-- Insert Site Settings
INSERT INTO SiteSettings (SettingKey, SettingValue, Description) VALUES
('site-title', 'Seonyx Holdings', 'Main site title'),
('site-tagline', 'Building Tomorrow''s Businesses Today', 'Site tagline/slogan'),
('contact-email', 'contact@seonyx.com', 'Email address for contact form submissions'),
('admin-username', 'admin', 'Admin username for CMS'),
('items-per-page', '10', 'Pagination default');
