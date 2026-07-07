export const environment = {
  production: false,
  // Relative so it works same-origin in every context: proxied to localhost:5000 by
  // proxy.conf.json during `ng serve`, and same-origin against the API in production
  // (Angular is built into the API's own wwwroot and served from the same host).
  apiBaseUrl: '/api'
};
