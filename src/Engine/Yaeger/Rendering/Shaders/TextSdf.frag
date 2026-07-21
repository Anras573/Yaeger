#version 330 core
in vec2 vTexCoord;
in vec4 vColor;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    float dist  = texture(uTexture, vTexCoord).r;
    float width = fwidth(dist);
    float alpha = smoothstep(0.5 - width, 0.5 + width, dist);
    FragColor = vec4(vColor.rgb, vColor.a * alpha);
}
