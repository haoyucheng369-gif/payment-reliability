FROM node:22-alpine AS build
WORKDIR /app

COPY PaymentFlowCloud.Web/package*.json ./
RUN npm ci

COPY PaymentFlowCloud.Web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY PaymentFlowCloud.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
