FROM nginx:alpine

WORKDIR /usr/share/nginx/html

# Supprimer les pages par défaut de Nginx et copier les fichiers Blazor WASM publiés
RUN rm -rf ./*
COPY wwwroot/ .

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]