// Demo request form: POSTs JSON to `${data-api}/leads`. Typed values are never cleared on failure.
const form = document.getElementById('demo');
if (form) {
  const status = form.querySelector('[role="status"]');
  const button = form.querySelector('button[type="submit"]');

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const f = form.elements;
    const body = {
      venueName: f.venueName.value.trim(),
      contactName: f.contactName.value.trim(),
      phone: f.phone.value.trim(),
      city: f.city.value.trim(),
      courts: Number(f.courts.value),
      message: f.message.value.trim(),
      language: document.documentElement.lang,
      website: f.website.value,
    };
    button.disabled = true;
    status.textContent = '';
    try {
      const res = await fetch(`${form.dataset.api}/leads`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (res.status === 202) {
        const thanks = document.createElement('p');
        thanks.className = 'thanks';
        thanks.setAttribute('role', 'status');
        thanks.textContent = form.dataset.success;
        form.replaceWith(thanks);
        return;
      }
      let message = form.dataset.error;
      if (res.status === 400) {
        const problem = await res.json().catch(() => null);
        // The API writes errors in English; other languages show their own sentence.
        message = form.dataset.invalid || (problem && problem.title) || message;
      }
      status.textContent = message;
    } catch {
      status.textContent = form.dataset.error;
    }
    button.disabled = false;
  });
}
