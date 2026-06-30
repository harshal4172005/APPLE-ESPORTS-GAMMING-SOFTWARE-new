# Build Stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package*.json ./
RUN npm ci --legacy-peer-deps

# Copy the rest of the application
COPY . .

# Build the Vite application for production
RUN npm run build

# Nginx Stage (serve static files)
FROM nginx:alpine
# Copy the built assets to Nginx html directory
COPY --from=build /app/dist /usr/share/nginx/html

# We don't need a custom nginx.conf here if we are using a central proxy,
# but we need fallback routing for React Router.
RUN echo "server { \
    listen 80; \
    location / { \
        root /usr/share/nginx/html; \
        index index.html; \
        try_files \$uri \$uri/ /index.html; \
    } \
}" > /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
