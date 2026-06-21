# Multi-stage build for an ARD treasure-hunt server (Azure Container Apps).
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production
COPY package*.json ./
RUN npm ci --omit=dev && npm cache clean --force
COPY --from=build /app/dist ./dist
# Container Apps sets PORT; the servers read process.env.PORT.
ENV PORT=8080
EXPOSE 8080
CMD ["npm", "start"]
