#include "pch.h"
#include "texture_helper.h"
#include "WICTextureLoader.h"

GLuint setup_texture(LPTSTR fileName)
{
	std::wstring widePath = Windows::ApplicationModel::Package::Current->InstalledLocation->Path->Data();

	GLuint mTexture;

	// Load the texture
	glGenTextures(1, &mTexture);
	glBindTexture(GL_TEXTURE_2D, mTexture);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	HRESULT hr = WICTexImage2DFromFile(GL_TEXTURE_2D, 0, (widePath + L"\\Telegram.Intro\\Assets\\" + fileName).c_str());

	auto failed = FAILED(hr);

	return mTexture;
}
