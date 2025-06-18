# CloudNativeEstimationHelper

## CORS Configuration

This application includes CORS (Cross-Origin Resource Sharing) configuration to allow all calls to the website. The configuration is stored in the `wwwroot/cors.json` file with the following settings:

- **Allow all origins**: `*` 
- **Allowed methods**: GET, POST, PUT, DELETE, OPTIONS
- **Allowed headers**: Content-Type, Authorization, X-Requested-With
- **Allow credentials**: true
- **Max age**: 86400 seconds (24 hours)

The CORS configuration is applied to all outgoing HTTP requests using the `CorsService` which is initialized at application startup.