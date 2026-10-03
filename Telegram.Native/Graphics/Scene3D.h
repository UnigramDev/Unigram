#pragma once

#include <winrt/base.h>

#include <d3d11_1.h>

#include <cstdint>
#include <memory>
#include <string>
#include <utility>
#include <vector>

namespace Graphics3D
{
    // Column-major, so the bytes reach the GPU in the order the translated shaders expect and the
    // arithmetic reads the same as the GLSL it came from. Do not "fix" this to row-major without
    // changing #pragma pack_matrix in every shader that reads one.
    struct Matrix4
    {
        float m[16] = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        float& at(int column, int row) { return m[column * 4 + row]; }
        float at(int column, int row) const { return m[column * 4 + row]; }

        static Matrix4 RotationX(float radians);
        static Matrix4 RotationY(float radians);
        static Matrix4 RotationZ(float radians);

        Matrix4 Transposed() const;
        Matrix4 operator*(const Matrix4& other) const;
    };

    /// <summary>
    /// How a scene turns: at rest, under the pointer, and on release.
    /// </summary>
    /// <remarks>
    /// The mechanism is the host's and the numbers are the scene's, because every one of them was
    /// read out of the app being matched rather than chosen. A scene that should not be turned at
    /// all asks for no spin and no drag, and then nothing in the host moves it.
    /// </remarks>
    struct SceneMotion
    {
        // Seconds for one full turn at rest. Zero for a scene that does not spin.
        float secondsPerTurn = 0;

        // How long the scene is held still before the idle spin takes over, counted from when it
        // was first shown. A scene whose authored look faces the viewer wants to be seen that way
        // before it starts turning away.
        float spinStartDelaySeconds = 0;

        // How quickly a kick runs out of momentum, as a time constant: after this long about a
        // third of it is left, and after three times it nothing. Zero ignores kicks entirely.
        float kickDecaySeconds = 0;

        // Degrees per pixel dragged. Zero for a scene the pointer should not turn.
        float dragYawPerPixel = 0;
        float dragPitchPerPixel = 0;

        // How far the pointer can tip it, as a **soft** limit: the tilt approaches this and
        // never reaches it, so the drag meets more resistance the further it goes. Zero for a
        // scene whose tilt should be linear and unbounded.
        float pitchSoftLimitDegrees = 0;

        // How long the release takes to spring back.
        float settleSeconds = 0.35f;

        // Whether the idle spin runs unless something turns it off. Off is the common answer: a
        // scene whose highlights are authored for square-on loses them while it turns.
        bool spinsByDefault = false;
    };

    /// <summary>
    /// Where the scene stands this frame.
    /// </summary>
    /// <remarks>
    /// Degrees, because that is what the motion constants are in and what the hosts being matched
    /// accumulate. A scene converts once, where it builds its matrices.
    /// </remarks>
    struct ScenePose
    {
        float yaw = 0;
        float pitch = 0;

        // The third axis, which the diamond gained in beta 11. Nothing drives it yet: the field
        // the beta reads it from is written somewhere the dex tooling could not follow across
        // objects, so it stays at rest until that is found or someone asks for it.
        float roll = 0;

        // Seconds the scene has been shown, which stops while it is off screen so that a baked
        // animation resumes where it left rather than jumping.
        float time = 0;

        // Seconds since the last frame, capped the way the app being matched caps it, and the
        // signed yaw rate over that step - which anything reacting to being turned reads.
        float step = 0;
        float yawPerSecond = 0;

        std::uint32_t width = 0;
        std::uint32_t height = 0;

        float Aspect() const { return height > 0 ? static_cast<float>(width) / height : 1.0f; }
    };

    /// <summary>
    /// What the host hands over to draw into: the back buffer, already cleared to transparent.
    /// </summary>
    /// <remarks>
    /// Transparent rather than a background colour, because the panel is composed over whatever
    /// the screen puts behind it. A scene that needs intermediate targets owns them itself - the
    /// diamond's fade pass is one scene's answer, not a shape to build into every scene.
    /// </remarks>
    struct SceneTarget
    {
        ID3D11DeviceContext* context = nullptr;
        ID3D11RenderTargetView* view = nullptr;
        std::uint32_t width = 0;
        std::uint32_t height = 0;
    };

    /// <summary>
    /// What every instance of a scene on one device can share: shaders, immutable buffers,
    /// pipeline state - whatever does not change from one panel to the next.
    /// </summary>
    /// <remarks>
    /// One per device and emptied when the device is replaced, so nothing in it is handed to a
    /// scene on another device. Render thread only, like everything a scene is given, and nothing
    /// put in may be written afterwards: every panel's frame reads it.
    /// </remarks>
    class SceneCache
    {
    public:
        // Keyed by type. A failed create is not remembered, so the next scene to load tries again.
        template <typename T, typename Create>
        std::shared_ptr<T> GetOrCreate(Create&& create)
        {
            // Not const: identical read-only data can be folded by the linker into one address,
            // which would give two types the same key.
            static char key;

            for (auto& entry : m_entries)
            {
                if (entry.first == &key)
                {
                    return std::static_pointer_cast<T>(entry.second);
                }
            }

            std::shared_ptr<T> value = create();

            if (value != nullptr)
            {
                m_entries.emplace_back(&key, value);
            }

            return value;
        }

        void Clear() noexcept
        {
            m_entries.clear();
        }

    private:
        std::vector<std::pair<const void*, std::shared_ptr<void>>> m_entries;
    };

    /// <summary>
    /// One drawable thing, with its shaders, its buffers and its own idea of what a frame is.
    /// </summary>
    /// <remarks>
    /// The seam sits here rather than at "a mesh" deliberately. The two 3D families in the app
    /// being matched share no shader, no vertex layout and no uniform block: the diamond is a
    /// hull with authored facet optics and a baked 42-float animation table, while the star, the
    /// coin and the card are textured meshes with a normal map and an MVP matrix. What they do
    /// share is everything in <see cref="Scene3DRenderer"/> - a device, a swap chain sized to a
    /// panel, a frame clock, and a pointer that turns them - so that is what was made generic.
    ///
    /// Every method runs on the render thread, one at a time, and never on the UI thread.
    /// </remarks>
    struct IScene3D
    {
        virtual ~IScene3D() = default;

        virtual SceneMotion Motion() const = 0;

        // False leaves the panel blank and is final: the host does not retry. The error is for a
        // log, not for the user, because there is nothing they can do about a missing asset.
        //
        // Anything the scene's other instances could use as they are belongs in the cache.
        virtual bool Load(ID3D11Device* device, SceneCache& cache, std::wstring& error) = 0;

        // Can arrive before Load, because the host only learns the size from a layout pass and
        // only loads once it has one. Targets are all that may be built here for that reason.
        virtual void Resize(ID3D11Device* device, std::uint32_t width, std::uint32_t height) = 0;

        virtual void Render(const SceneTarget& target, const ScenePose& pose) = 0;

        // Which look to wear, for a scene authored with more than one. Not pure: most scenes have
        // exactly one and should not have to say so.
        virtual void SetVariant(int variant) {}
    };
}
