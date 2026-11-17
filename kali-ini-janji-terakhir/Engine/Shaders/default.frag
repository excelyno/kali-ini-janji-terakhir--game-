#version 330 core
out vec4 FragColor;

in vec2 TexCoord;
in vec3 FragPos;

uniform sampler2D texture0;
uniform vec3 lightPos;
uniform vec3 lightColor;
uniform vec3 ambientColor;

void main()
{
    vec4 texColor = texture(texture0, TexCoord);
    if(texColor.a < 0.1)
        discard;
        
    // Pencahayaan 2.5D sederhana berbasis jarak dari sumber cahaya
    float distance = length(lightPos - FragPos);
    float attenuation = 1.0 / (1.0 + 0.005 * distance + 0.0001 * (distance * distance));
    
    vec3 diffuse = lightColor * attenuation;
    vec3 finalColor = (ambientColor + diffuse) * texColor.rgb;
    
    FragColor = vec4(finalColor, texColor.a);
}
