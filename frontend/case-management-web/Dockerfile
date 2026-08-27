FROM node:22-alpine AS build
WORKDIR /source

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/dist/case-management-web/browser /usr/share/nginx/html
EXPOSE 80
