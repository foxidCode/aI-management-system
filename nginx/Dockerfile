FROM nginx:alpine
COPY nginx.conf /etc/nginx/nginx.conf
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 CMD wget -qO- http://localhost:80/ || exit 1
