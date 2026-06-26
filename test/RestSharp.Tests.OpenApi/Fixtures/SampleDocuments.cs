namespace RestSharp.Tests.OpenApi.Fixtures;

/// <summary>OpenAPI documents used across the tests, kept inline so the suite is self-contained.</summary>
static class SampleDocuments {
    /// <summary>
    /// A small but feature-rich Pet Store: path/query/header parameters with constraints, a JSON
    /// request body, several response media types, and a polymorphic schema (Pet -&gt; Dog/Cat via
    /// allOf + discriminator).
    /// </summary>
    public const string PetStore =
        """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pet Store", "version": "1.2.3" },
          "servers": [ { "url": "https://api.example.com/v1" } ],
          "paths": {
            "/pets/{petId}": {
              "parameters": [
                { "name": "petId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int64", "minimum": 1 } }
              ],
              "get": {
                "operationId": "getPetById",
                "parameters": [
                  { "name": "X-Trace-Id", "in": "header", "required": false, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } },
                      "application/xml":  { "schema": { "$ref": "#/components/schemas/Pet" } }
                    }
                  },
                  "404": { "description": "not found" }
                }
              },
              "delete": {
                "operationId": "deletePet",
                "responses": { "204": { "description": "deleted" } }
              }
            },
            "/pets": {
              "get": {
                "operationId": "listPets",
                "parameters": [
                  { "name": "limit",  "in": "query", "required": false, "schema": { "type": "integer", "minimum": 1, "maximum": 100 } },
                  { "name": "status", "in": "query", "required": false, "schema": { "type": "string", "enum": ["available", "pending", "sold"] } },
                  { "name": "tags",   "in": "query", "required": false, "schema": { "type": "array", "items": { "type": "string" } } }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": { "application/json": { "schema": { "type": "array", "items": { "$ref": "#/components/schemas/Pet" } } } }
                  }
                }
              },
              "post": {
                "operationId": "createPet",
                "requestBody": {
                  "required": true,
                  "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } }
                },
                "responses": {
                  "201": {
                    "description": "created",
                    "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Pet": {
                "type": "object",
                "required": ["id", "name", "petType"],
                "discriminator": {
                  "propertyName": "petType",
                  "mapping": { "dog": "#/components/schemas/Dog", "cat": "#/components/schemas/Cat" }
                },
                "properties": {
                  "id":      { "type": "integer", "format": "int64" },
                  "name":    { "type": "string", "minLength": 1, "maxLength": 50 },
                  "petType": { "type": "string" },
                  "tags":    { "type": "array", "items": { "type": "string" } }
                }
              },
              "Dog": {
                "allOf": [
                  { "$ref": "#/components/schemas/Pet" },
                  { "type": "object", "properties": { "bark": { "type": "boolean" }, "breed": { "type": "string" } } }
                ]
              },
              "Cat": {
                "allOf": [
                  { "$ref": "#/components/schemas/Pet" },
                  { "type": "object", "properties": { "huntingSkill": { "type": "string" } } }
                ]
              }
            }
          }
        }
        """;

    /// <summary>A document that omits operationId and base path, to exercise attribute-based binding.</summary>
    public const string NoOperationIds =
        """
        {
          "openapi": "3.0.3",
          "info": { "title": "Minimal", "version": "1.0.0" },
          "paths": {
            "/things/{id}": {
              "get": {
                "parameters": [ { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              }
            }
          }
        }
        """;
}
