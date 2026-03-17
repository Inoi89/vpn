FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL
ARG VITE_ACCESS_TOKEN
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_ACCESS_TOKEN=${VITE_ACCESS_TOKEN}

COPY frontend/control-plane-ui/package*.json ./
RUN npm install

COPY frontend/control-plane-ui ./
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
