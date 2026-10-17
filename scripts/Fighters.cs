using Godot;
using System;
using System.Collections.Generic;

public partial class Fighters : CharacterBody2D {
	public const float Speed = 200.0f;
	public const float Acceleration = 1000.0f;
	public const float Deceleration = 1500.0f;
	public const float JumpVelocity = -250.0f;
	public const float JumpCutMultiplier = 0.5f;

	public const float AttackAccelMultiplier = 0.5f;
	public const float AttackDecelMultiplier = 0.25f;
	private const float AttackBufferTime = 0.15f;

	// ---------- Attack data (numbers come from the frame data workbook) ----------

	private enum AttackType {
		GroundNeutral,
		// GroundSide, GroundUp, GroundDown, AirNeutral, AirSide, AirUp, AirDown come later
	}

	private class AttackData {
		public string Animation;
		public Vector2 Size;
		public Vector2 Offset;
		public int StartFrame;
		public int EndFrame;
		public int Damage;
		public float Mobility;   // 0 = commits you, 1 = keeps nearly full movement

		public AttackData(string animation, Vector2 size, Vector2 offset,
						  int startFrame, int endFrame, int damage, float mobility) {
			Animation = animation; Size = size; Offset = offset;
			StartFrame = startFrame; EndFrame = endFrame;
			Damage = damage; Mobility = mobility;
		}
	}

	private static readonly Dictionary<AttackType, AttackData> Attacks = new() {
		// Neutral Attack (jab): startup 2, active 2, recovery 4 = 8 frames
		{ AttackType.GroundNeutral,
		  new AttackData("attack", new Vector2(16, 14), new Vector2(14, 0), 2, 3, 4, 1.0f) },
	};

	private AnimatedSprite2D animatedSprite;
	private Area2D attackHitbox;
	private Sprite2D hitboxVisual;
	private CollisionShape2D hitboxShape;
	private RectangleShape2D hitboxRect;
	
	private bool isAttacking = false;
	private readonly HashSet<Node> alreadyHit = new();
	private bool facingRight = true;
	private float attackBufferTimer = 0f;
	private AttackData currentAttack = Attacks[AttackType.GroundNeutral];
	
	public override void _Ready() {
		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		attackHitbox = GetNode<Area2D>("AttackHitbox");
		hitboxShape = attackHitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		hitboxVisual = attackHitbox.GetNode<Sprite2D>("HitboxVisual");

		// Hitbox area
		hitboxRect = new RectangleShape2D();
		hitboxShape.Shape = hitboxRect;
		hitboxShape.Position = Vector2.Zero;
		hitboxVisual.Position = Vector2.Zero;
		hitboxVisual.Visible = false;

		attackHitbox.Monitoring = false;
		attackHitbox.AreaEntered += OnHitboxAreaEntered;
		animatedSprite.FrameChanged += OnFrameChanged;
		animatedSprite.AnimationFinished += OnAnimationFinished;

		SetFacing(true);
		animatedSprite.Play("idle");
	}
	
	public override void _PhysicsProcess(double delta) {
		Vector2 velocity = Velocity;

		// Gravity
		if (!IsOnFloor()) {
			velocity += GetGravity() * (float)delta;
		}
		
		Velocity = velocity;

		// Attack
		if (Input.IsActionJustPressed("attack") && !isAttacking) {
			attackBufferTimer = AttackBufferTime;
		}
		attackBufferTimer -= (float)delta;

		// Grounded Attacks
		if (attackBufferTimer > 0 && !isAttacking && IsOnFloor()) {
			attackBufferTimer = 0;
			StartAttack();
		}

		// Jump
		Jump();

		// Horizontal movement
		float direction = Input.GetAxis("move_left", "move_right");
		Vector2 dir = new Vector2(direction, 0);

		Walk(dir, (float)delta);
		MoveAndSlide();
	}

	private void Walk(Vector2 dir, float delta) {
		Vector2 velocity = Velocity;

		float accel = Acceleration;
		float decel = Deceleration;
		if (isAttacking) {
			accel *= AttackAccelMultiplier * currentAttack.Mobility;
			decel *= Mathf.Lerp(1f, AttackDecelMultiplier, currentAttack.Mobility);
		}

		if (dir.X != 0) {
			SetFacing(dir.X > 0);
			velocity.X = Mathf.MoveToward(velocity.X, dir.X * Speed, accel * delta);
		} else {
			velocity.X = Mathf.MoveToward(velocity.X, 0, decel * delta);
		}

		Velocity = velocity;
	}

	private void Jump() {
		if (Input.IsActionJustPressed("jump") && IsOnFloor()) {
			Velocity = new Vector2(Velocity.X, JumpVelocity);
		}
		if (Input.IsActionJustReleased("jump") && Velocity.Y < 0) {
			Velocity = new Vector2(Velocity.X, Velocity.Y * JumpCutMultiplier);
		}
	}

	// Attack functions
	private AttackType ChooseAttack() {
		return AttackType.GroundNeutral;
	}

	private void StartAttack() {
		float direction = Input.GetAxis("move_left", "move_right");
		currentAttack = Attacks[ChooseAttack()];
		
		SetFacing(direction != 0 ? direction > 0 : facingRight);

		isAttacking = true;
		alreadyHit.Clear();

		// Shapes the hitbox
		hitboxRect.Size = currentAttack.Size;
		hitboxVisual.Scale = currentAttack.Size / hitboxVisual.Texture.GetSize();

		animatedSprite.Play(currentAttack.Animation);
	}

	private void EndAttack() {
		isAttacking = false;
		attackHitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		animatedSprite.Play("idle");
		hitboxVisual.Visible = false;
	}

	// Hitbox is only live on the damaging frames
	private void OnFrameChanged() {
		if (!isAttacking) return;
		bool active = animatedSprite.Frame >= currentAttack.StartFrame
				   && animatedSprite.Frame <= currentAttack.EndFrame;
		attackHitbox.SetDeferred(Area2D.PropertyName.Monitoring, active);
		hitboxVisual.Visible = active;
	}

	private void OnAnimationFinished() {
		if (isAttacking) EndAttack();
	}

	private void OnHitboxAreaEntered(Area2D area) {
		Node target = area.GetParent();
		if (target == this || alreadyHit.Contains(target)) return; // one hit per swing
		alreadyHit.Add(target);

		if (target.HasMethod("TakeDamage")) {
			target.Call("TakeDamage", currentAttack.Damage);
		}
	}

	private void SetFacing(bool right) {
		if (isAttacking) return;
		
		facingRight = right;
		animatedSprite.FlipH = !right;

		Vector2 offset = currentAttack.Offset;
		attackHitbox.Position = new Vector2(right ? offset.X : -offset.X, offset.Y);
	}
}
