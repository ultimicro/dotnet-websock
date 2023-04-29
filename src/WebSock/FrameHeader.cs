namespace WebSock;

/// <summary>
/// Represents a frame header.
/// </summary>
public struct FrameHeader
{
    private byte value;

    public FrameHeader(byte value)
    {
        this.value = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this is the final fragment in a message.
    /// </summary>
    /// <remarks>
    /// The first fragment MAY also be the final fragment.
    /// </remarks>
    public bool Fin
    {
        get => (this.value & 0x01) != 0;
        set
        {
            if (value)
            {
                this.value |= 0x01;
            }
            else
            {
                this.value &= 0xfe;
            }
        }
    }

    public bool Rsv1
    {
        get => (this.value & 0x02) != 0;
        set
        {
            if (value)
            {
                this.value |= 0x02;
            }
            else
            {
                this.value &= 0xfd;
            }
        }
    }

    public bool Rsv2
    {
        get => (this.value & 0x04) != 0;
        set
        {
            if (value)
            {
                this.value |= 0x04;
            }
            else
            {
                this.value &= 0xfb;
            }
        }
    }

    public bool Rsv3
    {
        get => (this.value & 0x08) != 0;
        set
        {
            if (value)
            {
                this.value |= 0x08;
            }
            else
            {
                this.value &= 0xf7;
            }
        }
    }

    /// <summary>
    /// Gets or sets the interpretation of the "Payload data".
    /// </summary>
    public Opcode Opcode
    {
        get => (Opcode)((this.value & 0xf0) >> 4);
        set => this.value |= (byte)(((byte)value) << 4);
    }

    public byte Value => this.value;
}
