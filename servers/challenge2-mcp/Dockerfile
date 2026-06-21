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
# Container Apps injects PORT; the servers read process.env.PORT (this is just the local default).
ENV PORT=8080
EXPOSE 8080
# Drop root: the official node image ships a non-root `node` user (uid 1000).
USER node
# Exec form so Node is PID 1 and receives SIGTERM directly (graceful shutdown).
CMD ["node", "dist/index.js"]
