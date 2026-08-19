#version 330 core
in vec2 vTexCoord;
in vec4 vColor;
out vec4 FragColor;

// Unlit: particles are their own light source (glows, sparks, embers) or read as flat colour
// (smoke, dust), so no scene lighting is sampled here — matches how UiRenderer/SkyboxRenderer
// stay outside Renderer3D's lit main pass.
uniform sampler2D uTexture;

void main() {
    FragColor = texture(uTexture, vTexCoord) * vColor;
}
