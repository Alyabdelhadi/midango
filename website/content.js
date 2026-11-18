// Operator-edited content: GETs `${body data-api}/site/content?lang=<html lang>` and swaps it into the page.
// The static HTML is the fallback: on any error, or for any missing value, the page stays as written.
// Every piece of API text goes in through textContent; links are built element by element and only after validation.
(async () => {
  const api = document.body.dataset.api;
  if (!api) return;
  const lang = document.documentElement.lang === 'ar' ? 'ar' : 'en';
  const ar = lang === 'ar';

  let data;
  try {
    const res = await fetch(`${api}/site/content?lang=${lang}`);
    if (!res.ok) return;
    data = await res.json();
  } catch {
    return;
  }
  if (!data || typeof data !== 'object') return;

  const str = (v) => (typeof v === 'string' && v.trim() ? v.trim() : null);
  const https = (v) => { const s = str(v); return s && /^https:\/\/\S+$/i.test(s) ? s : null; };
  const link = (href, text, external) => {
    const a = document.createElement('a');
    a.href = href;
    a.textContent = text;
    if (external) { a.target = '_blank'; a.rel = 'noopener'; }
    return a;
  };

  // Texts.
  const texts = data.texts && typeof data.texts === 'object' ? data.texts : {};
  document.querySelectorAll('[data-text]').forEach((el) => {
    const value = str(texts[el.dataset.text]);
    if (value) el.textContent = value;
  });

  // FAQ: replaced only when the API has at least one complete pair.
  const faqs = (Array.isArray(data.faqs) ? data.faqs : [])
    .map((f) => f && { q: str(f.question), a: str(f.answer) })
    .filter((f) => f && f.q && f.a);
  const faqList = document.querySelector('.faq-list');
  if (faqList && faqs.length) {
    faqList.replaceChildren(...faqs.map(({ q, a }) => {
      const details = document.createElement('details');
      const summary = document.createElement('summary');
      const p = document.createElement('p');
      summary.textContent = q;
      p.textContent = a;
      details.append(summary, p);
      return details;
    }));
  }

  // Footer contact line: only the details that are set and valid; hidden when none are.
  const contact = document.querySelector('.site-footer .contact');
  const c = data.contact && typeof data.contact === 'object' ? data.contact : {};
  if (contact) {
    const links = [];
    const email = str(c.email);
    if (email && /^[^\s@<>"]+@[^\s@<>"]+\.[^\s@<>"]+$/.test(email)) links.push(link(`mailto:${email}`, email));
    const phone = str(c.phone);
    if (phone && /^\+?[0-9 ]{5,30}$/.test(phone)) {
      const a = link(`tel:${phone.replace(/ /g, '')}`, phone);
      a.dir = 'ltr';
      links.push(a);
    }
    const whatsApp = str(c.whatsApp);
    const waDigits = whatsApp && /^\+?[0-9 ]+$/.test(whatsApp) ? whatsApp.replace(/\D/g, '') : '';
    if (waDigits.length >= 5) links.push(link(`https://wa.me/${waDigits}`, contact.dataset.whatsapp || 'WhatsApp', true));
    const instagram = str(c.instagram)?.replace(/^@/, '');
    if (instagram && /^[A-Za-z0-9._]{1,60}$/.test(instagram)) {
      const a = link(`https://instagram.com/${instagram}`, `@${instagram}`, true);
      a.dir = 'ltr';
      links.push(a);
    }
    if (links.length) {
      const label = document.createElement('span');
      label.textContent = contact.dataset.label || 'Contact';
      contact.replaceChildren(label, ...links);
      contact.hidden = false;
    }
  }

  // Store badges: a set URL turns the "coming soon" badge into a real link.
  const stores = {
    ios: { url: https(data.iosStoreUrl), small: ar ? 'حمّله من' : 'Download on the', name: 'App Store' },
    android: { url: https(data.androidStoreUrl), small: ar ? 'احصل عليه من' : 'Get it on', name: 'Google Play' },
  };
  for (const [key, s] of Object.entries(stores)) {
    const badge = document.querySelector(`.store[data-store="${key}"]`);
    if (!badge || !s.url) continue;
    const a = document.createElement('a');
    a.className = badge.className;
    a.dataset.store = key;
    a.href = s.url;
    a.target = '_blank';
    a.rel = 'noopener';
    a.setAttribute('aria-label', `${s.small} ${s.name}`);
    const svg = badge.querySelector('svg');
    const span = document.createElement('span');
    const small = document.createElement('small');
    const b = document.createElement('b');
    small.textContent = s.small;
    b.textContent = s.name;
    span.append(small, b);
    if (svg) a.append(svg);
    a.append(span);
    badge.replaceWith(a);
  }
  const note = document.querySelector('.stores-note');
  if (note && stores.ios.url && stores.android.url) note.hidden = true;
})();
