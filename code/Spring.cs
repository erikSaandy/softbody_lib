using System;

/// <summary>
/// Closed-form solution for the ODE of damped harmonic oscillator.
/// https://en.wikipedia.org/wiki/Harmonic_oscillator#Damped_harmonic_oscillator
///
/// Proof and derived from http://www.ryanjuckett.com/programming/damped-springs/
/// </summary>
public class Spring : Component
{
	// Default to critically damped
	public double damping = 26f;
	public double mass = 1f;
	public double stiffness = 169f;
	public double startValue;
	public double endValue;
	public double initialVelocity;

	protected double currentValue;
	protected double currentVelocity;

	protected double springTime;

	public override void Reset()
	{
		springTime = 0f;
		currentValue = 0f;
		currentVelocity = 0f;
	}

	public void UpdateEndValue( double value, double velocity )
	{
		startValue = currentValue;
		endValue = value;
		initialVelocity = velocity;
		springTime = 0f;
	}

	public double Evaluate( double deltaTime )
	{
		springTime += deltaTime;

		var v0 = -initialVelocity;
		var t = springTime;
		var zeta = damping / (2 * Math.Sqrt( stiffness * mass )); // damping ratio
		var omega0 = Math.Sqrt( stiffness / mass ); // undamped angular frequency of the oscillator (rad/s)
		var x0 = endValue - startValue;

		var omegaZeta = omega0 * zeta;
		double x = 0f;
		double v = 0f;

		if ( zeta < 1 ) // Under damped
		{
			var omega1 = omega0 * Math.Sqrt( 1.0f - zeta * zeta ); // exponential decay
			var e = Math.Exp( -omegaZeta * t );
			var c1 = x0;
			var c2 = (v0 + omegaZeta * x0) / omega1;
			var cos = Math.Cos( omega1 * t );
			var sin = Math.Sin( omega1 * t );
			x = e * (c1 * cos + c2 * sin);
			v = -e * ((x0 * omegaZeta - c2 * omega1) * cos + (x0 * omega1 + c2 * omegaZeta) * sin);
		}
		else if ( zeta > 1 ) // Over damped
		{
			var omega2 = omega0 * Math.Sqrt( zeta * zeta - 1.0f ); // frequency of damped oscillation
			var z1 = -omegaZeta - omega2;
			var z2 = -omegaZeta + omega2;
			var e1 = Math.Exp( z1 * t );
			var e2 = Math.Exp( z2 * t );
			var c1 = (v0 - x0 * z2) / (-2 * omega2);
			var c2 = x0 - c1;
			x = c1 * e1 + c2 * e2;
			v = c1 * z1 * e1 + c2 * z2 * e2;
		}
		else // Critically damped
		{
			var e = Math.Exp( -omega0 * t );
			x = e * (x0 + (v0 + omega0 * x0) * t);
			v = e * (v0 * (1 - t * omega0) + t * x0 * (omega0 * omega0));
		}

		currentValue = endValue - x;
		currentVelocity = v;

		return currentValue;
	}
}
