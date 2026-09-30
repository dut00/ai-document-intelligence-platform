# Included by the locations serving the UI (an add_header in a location drops the server-level ones).
# Not applied to proxied API responses: the Scalar UI needs inline scripts.
add_header Content-Security-Policy "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'" always;
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "no-referrer" always;
