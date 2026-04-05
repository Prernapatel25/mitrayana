const apiBase = "/api";

// ---------- Hide Preloader After Page Load ----------
window.addEventListener("load", function () {
  const preloader = document.getElementById("preloader");
  setTimeout(() => {
    if (preloader) preloader.classList.add("hide");
  }, 800); // Delay for smooth transition
});



// ---------- Fade-in effect for cards ----------
document.addEventListener("DOMContentLoaded", () => {
  const cards = document.querySelectorAll(".card");

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("visible");
        }
      });
    },
    { threshold: 0.2 }
  );

  cards.forEach((card) => observer.observe(card));
});

// ---------- Simulated login check ----------
const isLoggedIn = false; // change to true if user logged in

function applyForService(serviceType) {
  if (isLoggedIn) {
    // ✅ If logged in, go to next page
    window.location.href = `service-${serviceType}.html`;
  } else {
    // 🚫 If not logged in, show alert or modal
    alert("Please login first to apply for this service.");
    window.location.href = "login.html";
  }
}

// ---------- Preloader navigation effect ----------
document.querySelectorAll("a").forEach((link) => {
  link.addEventListener("click", (e) => {
    const target = link.getAttribute("href");
    if (target && !target.startsWith("#") && !link.target) {
      e.preventDefault();
      const preloader = document.getElementById("preloader");
      if (preloader) preloader.classList.remove("hide");
      setTimeout(() => (window.location.href = target), 500);
    }
  });
});

// Development note:
// Avoid running an example fetch automatically when the page is opened via file:// or when
// the backend is not reachable. That causes noisy "TypeError: Failed to fetch" errors.
if (window.location.protocol === 'file:') {
  console.warn('app.js warning: frontend loaded via file:// — fetch() to APIs will fail. Please access the application via a web server URL.');
  alert('Warning: This page is loaded via file://. API calls will fail. Please access via a web server URL.');
} else {
  // Optional: we could run a small health check here during development, but do not run
  // automatic POSTs that create data. Keep console output light.
  console.debug('app.js loaded — apiBase =', apiBase);
}
// Back-end base URL





/*
  app.js: central frontend helper for auth and API calls.
  Exposes global functions used by the static dashboard pages.
*/

// ---------- Helper Functions ----------
function getAuthHeaders() {
  const token = localStorage.getItem("token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

function requireAuth() {
  const token = localStorage.getItem("token");
  if (!token) {
    window.location.href = "/login.html";
    return false;
  }
  return true;
}

async function apiFetch(path, opts = {}) {
  opts.headers = Object.assign(
    opts.headers || {},
    { "Content-Type": "application/json" },
    getAuthHeaders()
  );
  const res = await fetch(`${apiBase}${path}`, opts);
  if (res.status === 401) {
    // token invalid/expired
    alert("Session expired or unauthorized. Please log in again.");
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    window.location.href = "/login.html";
    return null;
  }
  return res;
}

// ---------- Auth Handlers for Login/Register ----------
document.addEventListener("DOMContentLoaded", () => {
  const loginForm = document.getElementById("loginForm");

  // --- LOGIN ---
  if (loginForm) {
    loginForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      const email = document.getElementById("email").value;
      const password = document.getElementById("password").value;

      const res = await fetch(`${apiBase}/Auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => null);
        return alert(err?.message || "Login failed");
      }

      const data = await res.json();
      localStorage.setItem("token", data.token);
      localStorage.setItem("role", data.role);

      // navigate to role dashboard
      if (data.role === "Senior")
        window.location.href = "/senior-dashboard.html";
      else if (data.role === "ServiceProvider")
        window.location.href = "/serviceprovider-dashboard.html";
      else if (data.role === "Admin")
        window.location.href = "/admin-dashboard.html";
    });
  }

  // Register form is handled in register.html with custom UI
});

// ---------- Senior Functions ----------
async function createServiceRequest(title, description) {
  const body = { title, description };
  const res = await apiFetch("/Service", {
    method: "POST",
    body: JSON.stringify(body),
  });
  if (!res || !res.ok) return null;
  return res.json();
}

async function getMyRequests() {
  const res = await apiFetch("/Service/my", { method: "GET" });
  if (!res || !res.ok) return [];
  return res.json();
}

// ---------- ServiceProvider Functions ----------
async function getOpenRequests() {
  const res = await apiFetch("/Service/my", { method: "GET" });
  if (!res || !res.ok) return [];
  return res.json();
}

async function acceptRequest(requestId) {
  const res = await apiFetch(`/Service/accept/${requestId}`, { method: "POST" });
  if (!res || !res.ok) return false;
  return true;
}

// ---------- Feedback ----------
async function submitFeedback(message, rating) {
  const res = await apiFetch("/Feedback", {
    method: "POST",
    body: JSON.stringify({ message, rating }),
  });
  if (!res) throw new Error('Network error');
  if (!res.ok) {
    const errorText = await res.text();
    throw new Error(`HTTP ${res.status}: ${errorText}`);
  }
  return res.json();
}

async function getFeedbacks() {
  const res = await apiFetch("/Feedback", { method: "GET" });
  if (!res || !res.ok) return [];
  return res.json();
}

// ---------- Admin ----------
async function getUsers() {
  const res = await apiFetch("/Admin/users", { method: "GET" });
  if (!res || !res.ok) return [];
  return res.json();
}

async function getReports() {
  const res = await apiFetch("/Admin/reports", { method: "GET" });
  if (!res || !res.ok) return null;
  return res.json();
}

// ---------- Logout ----------
function logout() {
  localStorage.removeItem("token");
  localStorage.removeItem("role");
  window.location.href = "/";
}

// ---------- Scroll to Top Button ----------
window.onscroll = function() {scrollFunction()};

function scrollFunction() {
    const btn = document.getElementById("scrollToTopBtn");
    if (btn) {
        if (document.body.scrollTop > 20 || document.documentElement.scrollTop > 20) {
            btn.style.display = "block";
        } else {
            btn.style.display = "none";
        }
    }
}

function topFunction() {
    window.scrollTo({top: 0, behavior: 'smooth'});
}

// ---------- Expose Globals ----------
window.app = {
  getAuthHeaders,
  requireAuth,
  createServiceRequest,
  getMyRequests,
  getOpenRequests,
  acceptRequest,
  submitFeedback,
  getFeedbacks,
  getUsers,
  getReports,
  logout,
}; 